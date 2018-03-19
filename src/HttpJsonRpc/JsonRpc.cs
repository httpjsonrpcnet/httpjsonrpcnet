using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace HttpJsonRpc
{
    public static class JsonRpc
    {
        private static HttpListener Listener { get; set; }
        private static JsonRpcMethodCollection Methods { get; } = new JsonRpcMethodCollection();
        public static JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        private static List<Func<JsonRpcContext, Task>> OnReceivedRequestFuncs { get; } = new List<Func<JsonRpcContext, Task>>();

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

            if (address == null) address = "http://localhost:5000/";
            if (!address.EndsWith("/")) address += "/";

            Listener = new HttpListener();
            Listener.Prefixes.Add(address);
            Listener.Start();

            Console.WriteLine($"Listening for JSON-RPC requests on {address}");

            while (Listener.IsListening)
            {
                try
                {
                    var httpContext = await Listener.GetContextAsync();
                    HandleRequest(httpContext);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private static async void HandleRequest(HttpListenerContext httpContext)
        {
            if (!new[] { "GET", "POST" }.Contains(httpContext.Request.HttpMethod, StringComparer.InvariantCultureIgnoreCase))
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                httpContext.Response.OutputStream.Close();
                return;
            }

            JsonRpcRequest request = null;
            JsonRpcMethod method = null;
            var serializer = CreateSerializer();

            if (httpContext.Request.QueryString.Count > 0)
            {
                request = new JsonRpcRequest();

                var parameters = new Dictionary<string, string>();
                var queryString = httpContext.Request.QueryString;
                foreach (var key in queryString.AllKeys)
                {
                    if (key == null) continue; ;
                    var value = queryString[key];

                    if (key == "jsonrpc")
                    {
                        request.JsonRpc = value;
                        continue;
                    }

                    if (key == "id")
                    {
                        request.Id = value;
                        continue;
                    }

                    if (key == "method")
                    {
                        request.Method = value;
                        continue;
                    }

                    parameters[key] = value;
                }

                method = GetMethod(request.Method);

                //Move unknown values into ExtensionData
                if (method != null)
                {
                    var extensionData = new Dictionary<string, string>();
                    foreach (var parameter in parameters)
                    {
                        if (!method.Parameters.Contains(parameter.Key)) extensionData.Add(parameter.Key, parameter.Value);
                    }

                    foreach (var parameter in extensionData)
                    {
                        parameters.Remove(parameter.Key);
                    }

                    request.ExtensionData = JObject.FromObject(extensionData, serializer);
                }

                request.Params = JObject.FromObject(parameters, serializer);
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
                        request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson, SerializerSettings);
                    }
                    catch (Exception e)
                    {
                        await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.ParseError, null, e));
                        return;
                    }

                    method = GetMethod(request.Method);
                }
            }

            if (request?.Method == null)
            {
                var info = new JsonRpcInfo();
                info.Methods = Methods.ToList();
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromResult(null, info));
                return;
            }

            var jsonRpcContext = new JsonRpcContext(httpContext, request);
            JsonRpcContext.Current = jsonRpcContext;

            try
            {
                foreach (var f in OnReceivedRequestFuncs)
                {
                    await f(jsonRpcContext);
                }
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.InternalError, request.Id, e));
                return;
            }

            if (method == null)
            {
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.MethodNotFound, request.Id, request.Method));
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
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.ParseError, request.Id, e));
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
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.Unauthorized, request.Id, e));
                return;
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.ExecutionError, request.Id, e));
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

        private static async Task WriteResponseAsync(HttpListenerContext context, object result)
        {
            if (result is Stream resultStream)
            {
                context.Response.ContentType = "application/octet-stream";

                using (resultStream)
                {
                    await resultStream.CopyToAsync(context.Response.OutputStream);
                }
            }
            else
            {
                context.Response.ContentType = "application/json";
                var jsonResponse = JsonConvert.SerializeObject(result, SerializerSettings);
                var byteResponse = Encoding.UTF8.GetBytes(jsonResponse);
                await context.Response.OutputStream.WriteAsync(byteResponse, 0, byteResponse.Length);
            }

            context.Response.OutputStream.Close();
        }

        public static void Stop()
        {
            Listener?.Stop();
        }

        private static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(SerializerSettings);
        }
    }
}