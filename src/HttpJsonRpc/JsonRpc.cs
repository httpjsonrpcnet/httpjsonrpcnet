using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpc
    {
        public static IServiceProvider ServiceProvider { get; set; }
        public static Action<ServiceCollection> ConfigureServices { get; set; }

        public static JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        private static ILogger Logger { get; set; }
        private static HttpListener Listener { get; set; }
        private static JsonRpcMethodCollection Methods { get; } = new JsonRpcMethodCollection();
        private static List<Func<HttpListenerContext, Task>> OnReceivedHttpRequestFuncs { get; } = new List<Func<HttpListenerContext, Task>>();
        private static List<Func<JsonRpcContext, Task>> OnReceivedRequestFuncs { get; } = new List<Func<JsonRpcContext, Task>>();

        private JsonRpc()
        {
        }

        public static void OnReceivedHttpRequest(Func<HttpListenerContext, Task> func)
        {
            OnReceivedHttpRequestFuncs.Add(func);
        }

        public static void OnReceivedRequest(Func<JsonRpcContext, Task> func)
        {
            OnReceivedRequestFuncs.Add(func);
        }

        public static void RegisterMethods(Assembly fromAssembly)
        {
            if (fromAssembly == null) throw new ArgumentNullException(nameof(fromAssembly));

            foreach (var t in fromAssembly.DefinedTypes)
            {
                var classAttribute = t.GetCustomAttribute<JsonRpcClassAttribute>();
                var className = classAttribute?.Name ?? t.Name;

                foreach (var m in t.DeclaredMethods)
                {
                    var methodAttribute = m.GetCustomAttribute<JsonRpcMethodAttribute>();
                    if (methodAttribute == null) continue;

                    var name = methodAttribute.Name ?? $"{className}.{m.Name}";
                    var asyncIndex = name.LastIndexOf("Async", StringComparison.Ordinal);
                    if (asyncIndex > -1)
                    {
                        name = name.Remove(asyncIndex);
                    }

                    name = name.ToLowerInvariant();

                    var method = new JsonRpcMethod();
                    method.Name = name;
                    method.Description = methodAttribute.Description;
                    method.MethodInfo = m;

                    foreach (var parameterInfo in m.GetParameters())
                    {
                        var parameterAttribute = parameterInfo.GetCustomAttribute<JsonRpcParameterAttribute>();
                        if (parameterAttribute?.Ignore ?? false) continue;

                        var parameter = new JsonRpcParameter();
                        parameter.Name = parameterAttribute?.Name ?? parameterInfo.Name;
                        parameter.Description = parameterAttribute?.Description;
                        parameter.Type = JsonTypeMap.GetJsonType(parameterInfo.ParameterType);
                        parameter.Optional = parameterInfo.IsOptional;
                        method.Parameters.Add(parameter);
                    }

                    Methods.Add(method);
                }
            }
        }

        public static async void Start(string address = null)
        {
            if (Methods.Count == 0)
            {
                var excludeProducts = new List<string> {"Microsoft® .NET Framework", "Json.NET", "HttpJsonRpc"};

                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !excludeProducts.Contains(a.GetCustomAttribute<AssemblyProductAttribute>()?.Product));

                foreach (var assembly in assemblies)
                {
                    RegisterMethods(assembly);
                }
            }

            CreateServiceProvider();
            CreateLogger();

            if (address == null) address = "http://localhost:5000/";
            if (!address.EndsWith("/")) address += "/";

            Listener = new HttpListener();
            Listener.Prefixes.Add(address);
            Listener.Start();

            Logger?.LogInformation($"Listening for JSON-RPC requests on {address}");

            while (Listener.IsListening)
            {
                try
                {
                    var httpContext = await Listener.GetContextAsync();
                    HandleRequest(httpContext);
                }
                catch (Exception e)
                {
                    Logger?.LogError(e, "An error occured while accepting a request.");
                }
            }
        }

        private static async void HandleRequest(HttpListenerContext httpContext)
        {
            try
            {
                foreach (var f in OnReceivedHttpRequestFuncs)
                {
                    await f(httpContext);
                    if (httpContext.Response.ContentLength64 != 0) return;
                }
            }
            catch (Exception e)
            {
                await HandleErrorAsync(httpContext, JsonRpcErrorCodes.InternalError, null, e);
                return;
            }

            if (!new[] { "GET", "POST" }.Contains(httpContext.Request.HttpMethod, StringComparer.InvariantCultureIgnoreCase))
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                httpContext.Response.OutputStream.Close();
                return;
            }

            JObject jRequest = null;
            JsonRpcMethod method = null;
            var serializer = CreateSerializer();

            if (httpContext.Request.QueryString.Count > 0)
            {
                jRequest = new JObject();
                var parameters = new JObject();
                jRequest["params"] = parameters;

                var queryString = httpContext.Request.QueryString;
                foreach (var key in queryString.AllKeys)
                {
                    if (key == null) continue; ;
                    var value = queryString[key];

                    if (key == "jsonrpc" || key == "id" || key == "method")
                    {
                        jRequest[key] = value;
                        continue;
                    }

                    parameters[key] = value;
                }

                method = GetMethod(jRequest["method"].ToString());

                //Move unknown values into ExtensionData
                if (method != null)
                {
                    var extensionData = new JObject();
                    jRequest["extensionData"] = extensionData;

                    foreach (var parameter in parameters)
                    {
                        if (!method.Parameters.Contains(parameter.Key)) extensionData.Add(parameter.Key, parameter.ToString());
                    }

                    foreach (var parameter in extensionData)
                    {
                        parameters.Remove(parameter.Key);
                    }
                }
            }
            else
            {
                var contentType = httpContext.Request.ContentType?.ToLowerInvariant().Split(';')[0];
                if (contentType != null)
                {
                    string requestJson = null;

                    switch (contentType)
                    {
                        case "application/json":
                            using (var reader = new StreamReader(httpContext.Request.InputStream))
                            {
                                requestJson = await reader.ReadToEndAsync();
                            }
                            break;
                        case "multipart/form-data":
                            using (var reader = new StreamReader(httpContext.Request.InputStream))
                            {
                                var boundary = await reader.ReadLineAsync();
                                var multipartString = await reader.ReadToEndAsync();
                                var parts = multipartString.Split(new[] { boundary }, StringSplitOptions.RemoveEmptyEntries);
                                var requestPartHeader = "Content-Disposition: form-data; name=\"request\"";
                                var requestPart = parts.FirstOrDefault(p => p.StartsWith(requestPartHeader));

                                if (requestPart != null)
                                {
                                    requestJson = requestPart.Substring(requestPartHeader.Length).Trim();
                                }
                            }
                            break;
                        default:
                            httpContext.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                            httpContext.Response.OutputStream.Close();
                            return;
                    }

                    try
                    {
                        jRequest = JsonConvert.DeserializeObject<JObject>(requestJson, SerializerSettings);
                    }
                    catch (Exception e)
                    {
                        await HandleErrorAsync(httpContext, JsonRpcErrorCodes.ParseError, null, e);
                        return;
                    }

                    method = GetMethod(jRequest["method"].ToString());
                }
            }

            if (method == null)
            {
                var info = new JsonRpcInfo();
                info.Methods = Methods.ToList();
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromResult(null, info));
                return;
            }

            Logger?.LogInformation($"Recieved request:{Environment.NewLine}{jRequest}");
            //Logger?.LogInformation(jRequest.ToString());

            JsonRpcRequest request = null;
            try
            {
                request = jRequest.ToObject<JsonRpcRequest>(serializer);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(httpContext, JsonRpcErrorCodes.ParseError, null, e);
                return;
            }

            var jsonRpcContext = new JsonRpcContext(httpContext, request);
            JsonRpcContext.Current = jsonRpcContext;

            try
            {
                foreach (var f in OnReceivedRequestFuncs)
                {
                    await f(jsonRpcContext);
                    if (jsonRpcContext.HttpContext.Response.ContentLength64 != 0) return;
                }
            }
            catch (Exception e)
            {
                await HandleErrorAsync(httpContext, JsonRpcErrorCodes.InternalError, request, e);
                return;
            }

            //Prepare the method parameters
            var parameterValues = new List<object>();
            try
            {
                var parameters = method.MethodInfo.GetParameters();

                foreach (var parameter in parameters)
                {
                    var parameterAttribute = parameter.GetCustomAttribute<JsonRpcParameterAttribute>();
                    if (parameterAttribute?.Ignore == true)
                    {
                        parameterValues.Add(Type.Missing);
                        continue;
                    }

                    var parameterName = parameterAttribute?.Name ?? parameter.Name;
                    var value = request.Params?[parameterName]?.ToObject(parameter.ParameterType, serializer) ?? Type.Missing;
                    parameterValues.Add(value);
                }
            }
            catch (Exception e)
            {
                await HandleErrorAsync(httpContext, JsonRpcErrorCodes.ParseError, request, e);
                return;
            }

            //Execute the method
            try
            {
                try
                {
                    var methodTask = (Task) method.MethodInfo.Invoke(null, parameterValues.ToArray());
                    await methodTask;
                    var result = methodTask.GetType().GetProperty("Result")?.GetValue(methodTask);

                    if (!(result is Stream))
                    {
                        result = new JsonRpcResponse
                        {
                            Id = request.Id,
                            JsonRpc = "2.0",
                            Result = result
                        };
                    }

                    await WriteResponseAsync(httpContext, result);

                    return;
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            }
            catch (JsonRpcUnauthorizedException e)
            {
                await HandleErrorAsync(httpContext, JsonRpcErrorCodes.Unauthorized, request, e);
                return;
            }
            catch (Exception e)
            {
                await HandleErrorAsync(httpContext, JsonRpcErrorCodes.ExecutionError, request, e);
                return;
            }
        }

        private static JsonRpcMethod GetMethod(string name)
        {
            if (name == null) return null;

            name = name.ToLowerInvariant();
            if (!Methods.Contains(name)) return null;

            var method = Methods[name];

            return method;
        }

        private static async Task HandleErrorAsync(HttpListenerContext context, int errorCode, JsonRpcRequest request, object error)
        {
            try
            {
                Logger?.LogError(error?.ToString(), "An error occured while handling a request.");
                var response = JsonRpcResponse.FromError(errorCode, request?.Id, error);
                await WriteResponseAsync(context, response);
            }
            catch (Exception e)
            {
                Logger?.LogError(e, "An unexpected error occured while handling another error.");
            }
        }

        private static async Task WriteResponseAsync(HttpListenerContext context, object result)
        {
            var output = context.Response.OutputStream;

            if (result is Stream resultStream)
            {
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength64 = resultStream.Length;

                using (resultStream)
                {
                    await resultStream.CopyToAsync(output);
                }
            }
            else
            {
                context.Response.ContentType = "application/json";
                var jsonResponse = JsonConvert.SerializeObject(result, SerializerSettings);
                var byteResponse = Encoding.UTF8.GetBytes(jsonResponse);
                context.Response.ContentLength64 = byteResponse.Length;
                await output.WriteAsync(byteResponse, 0, byteResponse.Length);
            }

            output.Close();
        }

        public static void Stop()
        {
            Listener?.Stop();
        }

        private static void CreateServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(configure => configure.AddConsole());

            ConfigureServices?.Invoke(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        private static void CreateLogger()
        {
            Logger = ServiceProvider.GetService<ILogger<JsonRpc>>();
        }

        private static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(SerializerSettings);
        }
    }
}