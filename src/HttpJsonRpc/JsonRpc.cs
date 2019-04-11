using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using CommonServiceLocator;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpc
    {
        public static ILoggerFactory LoggerFactory { get; set; }

        public static JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        private static HttpListener Listener { get; } = new HttpListener();
        public static ICollection<string> Addresses => Listener.Prefixes;
        private static JsonRpcMethodCollection Methods { get; } = new JsonRpcMethodCollection();

        private static List<Func<HttpListenerContext, Task>> OnReceivedHttpRequestAsyncMethods { get; } = new List<Func<HttpListenerContext, Task>>();
        private static List<Func<JsonRpcContext, Task>> OnReceivedRequestAsyncMethods { get; } = new List<Func<JsonRpcContext, Task>>();
        private static List<Func<Exception, Task>> OnErrorAsyncMethods { get; } = new List<Func<Exception, Task>>();

        private static List<Action<HttpListenerContext>> OnReceivedHttpRequestMethods { get; } = new List<Action<HttpListenerContext>>();
        private static List<Action<JsonRpcContext>> OnReceivedRequestMethods { get; } = new List<Action<JsonRpcContext>>();
        private static List<Action<Exception>> OnErrorMethods { get; } = new List<Action<Exception>>();

        private JsonRpc()
        {
        }

        public static void OnReceivedHttpRequest(Func<HttpListenerContext, Task> method)
        {
            OnReceivedHttpRequestAsyncMethods.Add(method);
        }

        private static async Task OnReceivedHttpRequestAsync(HttpListenerContext context)
        {
            foreach (var method in OnReceivedHttpRequestAsyncMethods)
            {
                await method(context);
            }
        }

        public static void OnReceivedRequest(Func<JsonRpcContext, Task> method)
        {
            OnReceivedRequestAsyncMethods.Add(method);
        }

        private static async Task OnReceivedRequestAsync()
        {
            foreach (var method in OnReceivedRequestAsyncMethods)
            {
                await method(JsonRpcContext.Current);
            }
        }

        public static void OnReceivedHttpRequest(Action<HttpListenerContext> method)
        {
            OnReceivedHttpRequestMethods.Add(method);
        }

        private static void OnReceivedHttpRequest(HttpListenerContext context)
        {
            foreach (var method in OnReceivedHttpRequestMethods)
            {
                method(context);
            }
        }

        public static void OnReceivedRequest(Action<JsonRpcContext> method)
        {
            OnReceivedRequestMethods.Add(method);
        }

        private static void OnReceivedRequest()
        {
            foreach (var method in OnReceivedRequestMethods)
            {
                method(JsonRpcContext.Current);
            }
        }

        public static void OnErrorAsync(Func<Exception, Task> method)
        {
            OnErrorAsyncMethods.Add(method);
        }

        private static async Task OnErrorAsync(Exception e, string message)
        {
            var logger = CreateLogger();

            foreach (var method in OnErrorAsyncMethods)
            {
                try
                {
                    await method(e);
                }
                catch (Exception e2)
                {
                    logger?.LogError(e2, "An error occured while handling another error.");
                }
            }
        }

        public static void OnError(Action<Exception> method)
        {
            OnErrorMethods.Add(method);
        }

        private static void OnError(Exception e, string message)
        {
            var logger = CreateLogger();

            foreach (var method in OnErrorMethods)
            {
                try
                {
                    method(e);
                }
                catch (Exception e2)
                {
                    logger?.LogError(e2, "An error occured while handling another error.");
                }
            }

            logger?.LogError(e, message);
        }

        public static void RegisterMethods(Assembly fromAssembly)
        {
            if (fromAssembly == null) throw new ArgumentNullException(nameof(fromAssembly));

            foreach (var t in fromAssembly.DefinedTypes)
            {
                var classAttribute = t.GetCustomAttribute<JsonRpcClassAttribute>();
                if (classAttribute == null) continue;

                var className = classAttribute.Name ?? t.Name;
                foreach (var m in t.GetMethods())
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

        public static void Start(params string[] addresses)
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

            foreach (var address in addresses)
            {
                Addresses.Add(address.EndsWith("/") ? address : $"{address}/");
            }

            if (Addresses.Count == 0) Addresses.Add("http://127.0.0.1:5000/");

            Listener.Start();

            CreateLogger()?.LogInformation($"Listening for JSON-RPC requests on {string.Join(", ", Addresses)}");

            HandleRequests();
        }

        private static async void HandleRequests()
        {
            while (Listener.IsListening)
            {
                try
                {
                    var httpContext = await Listener.GetContextAsync();
                    JsonRpcContext.Current = new JsonRpcContext { HttpContext = httpContext };
                    HandleRequest();
                }
                catch (Exception e)
                {
                    var message = "An error occured while accepting a request.";
                    OnError(e, message);
                    await OnErrorAsync(e, message);
                }
            }
        }

        private static async void HandleRequest()
        {
            var context = JsonRpcContext.Current;
            var httpContext = context.HttpContext;

            try
            {
                OnReceivedHttpRequest(httpContext);
                await OnReceivedHttpRequestAsync(httpContext);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }

            try
            {
                if (httpContext.Response.ContentLength64 != 0) return;

                if (!new[] { "GET", "POST" }.Contains(httpContext.Request.HttpMethod, StringComparer.InvariantCultureIgnoreCase))
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    httpContext.Response.OutputStream.Close();
                    return;
                }

                if (httpContext.Request.RawUrl.EndsWith("favicon.ico"))
                {
                    httpContext.Response.OutputStream.Close();
                    return;
                }
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }

            try
            {
                await SetContextRequestJsonAsync();
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InvalidRequest, e);
                return;
            }

            try
            {
                SetContextRequest();
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.ParseError, e);
                return;
            }

            try
            {
                SetContextMethod();
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.MethodNotFound, e);
                return;
            }

            try
            {
                OnReceivedRequest();
                await OnReceivedRequestAsync();
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }

            if (httpContext.Response.ContentLength64 != 0) return;

            try
            {
                SetContextRequestParameters();
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.ParseError, e);
                return;
            }

            try
            {
                await ExecuteMethodAsync();
            }
            catch (JsonRpcUnauthorizedException e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.Unauthorized, e);
                return;
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }

            try
            {
                await WriteResponseAsync(context.Result);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }
        }

        private static async Task SetContextRequestJsonAsync()
        {
            var context = JsonRpcContext.Current;

            string requestJson = null;
            if (context.HttpContext.Request.QueryString.Count > 0)
            {
                requestJson = GetRequestFromQueryString();
            }
            else
            {
                var contentType = context.HttpContext.Request.ContentType?.ToLowerInvariant().Split(';')[0];
                if (contentType != null)
                {
                    switch (contentType)
                    {
                        case "application/json":
                            requestJson = await GetRequestFromBodyAsync();
                            break;
                        case "multipart/form-data":
                            requestJson = await GetRequestFromFormAsync();
                            break;
                        default:
                            throw new Exception($"{contentType} is not a supported content-type.");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(requestJson)) throw new Exception($"Request is empty.");

            context.RequestJson = requestJson;
        }

        private static async Task<string> GetRequestFromBodyAsync()
        {
            var httpContext = JsonRpcContext.Current.HttpContext;

            string jsonRequest;
            using (var reader = new StreamReader(httpContext.Request.InputStream))
            {
                jsonRequest = await reader.ReadToEndAsync();
            }

            return jsonRequest;
        }

        private static string GetRequestFromQueryString()
        {
            var httpContext = JsonRpcContext.Current.HttpContext;

            var jRequest = new JObject();
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

            //Move unknown values into ExtensionData
            var methodName = jRequest["method"]?.ToString()?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(methodName) && Methods.Contains(methodName))
            {
                var method = Methods[methodName];

                if (method != null)
                {
                    var extensionData = new Dictionary<string, string>();
                    foreach (var parameter in parameters)
                    {
                        if (!method.Parameters.Contains(parameter.Key)) extensionData[parameter.Key] = parameter.Value.ToString();
                    }

                    foreach (var kvp in extensionData)
                    {
                        parameters.Remove(kvp.Key);
                        jRequest[kvp.Key] = kvp.Value;
                    }
                }
            }

            return jRequest.ToString();
        }

        private static async Task<string> GetRequestFromFormAsync()
        {
            var httpContext = JsonRpcContext.Current.HttpContext;

            var jsonRequest = string.Empty;

            using (var reader = new StreamReader(httpContext.Request.InputStream))
            {
                var boundary = await reader.ReadLineAsync();
                var multipartString = await reader.ReadToEndAsync();
                var parts = multipartString.Split(new[] { boundary }, StringSplitOptions.RemoveEmptyEntries);
                var requestPartHeader = "Content-Disposition: form-data; name=\"request\"";
                var requestPart = parts.FirstOrDefault(p => p.StartsWith(requestPartHeader));

                if (requestPart != null)
                {
                    jsonRequest = requestPart.Substring(requestPartHeader.Length).Trim();
                }
            }

            return jsonRequest;
        }

        private static void SetContextRequest()
        {
            var context = JsonRpcContext.Current;

            context.Request = JsonConvert.DeserializeObject<JsonRpcRequest>(context.RequestJson, SerializerSettings);
            if (context.Request == null) throw new ArgumentException("Failed to parse JSON request.");
        }

        private static void SetContextMethod()
        {
            var context = JsonRpcContext.Current;
            var name = context.Request.Method;

            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("method is empty");

            name = name.ToLowerInvariant();
            if (!Methods.Contains(name)) throw new ArgumentException($"method {name} does not exist.");

            context.Method = Methods[name];
        }

        private static async Task ExecuteMethodAsync()
        {
            var context = JsonRpcContext.Current;
            var methodInfo = context.Method.MethodInfo;
            object classInstance = null;

            if (!methodInfo.IsStatic)
            {
                classInstance = ServiceLocator.IsLocationProviderSet ? ServiceLocator.Current.GetInstance(methodInfo.ReflectedType) : Activator.CreateInstance(methodInfo.ReflectedType);
            }

            try
            {
                var methodTask = (Task)context.Method.MethodInfo.Invoke(classInstance, context.RequestParameters.ToArray());
                await methodTask;
                context.Result = methodTask.GetType().GetProperty("Result")?.GetValue(methodTask);
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        private static void SetContextRequestParameters()
        {
            var context = JsonRpcContext.Current;

            var parameterInfos = context.Method.MethodInfo.GetParameters();
            var requestParameters = new List<object>();
            var serializer = CreateSerializer();

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                var parameter = parameterInfos[i];
                var parameterAttribute = parameter.GetCustomAttribute<JsonRpcParameterAttribute>();
                if (parameterAttribute?.Ignore == true)
                {
                    requestParameters.Add(Type.Missing);
                    continue;
                }

                var parameterName = parameterAttribute?.Name ?? parameter.Name;

                if (context.Request.Params.Type == JTokenType.Array)
                {
                    requestParameters.Add(context.Request.Params?[i]?.ToObject(parameter.ParameterType, serializer) ?? Type.Missing);
                }
                else
                {
                    requestParameters.Add(context.Request.Params?[parameterName]?.ToObject(parameter.ParameterType, serializer) ?? Type.Missing);
                }
            }

            context.RequestParameters = requestParameters;
        }

        private static async Task HandleErrorAsync(int errorCode, Exception error)
        {
            var request = JsonRpcContext.Current.Request;
            var jsonError = JsonRpcError.Create(errorCode, error);

            var message = $"An error occured while handling the request '{request?.Method}'. JsonRpcError: {jsonError.Message} ({jsonError.Code})";
            OnError(error, message);
            await OnErrorAsync(error, message);

            try
            {
                await WriteResponseAsync(null, jsonError);
            }
            catch (Exception e)
            {
                message = "An unexpected error occured while writing an error response.";
                OnError(e, message);
                await OnErrorAsync(e, message);
            }
        }

        private static async Task WriteResponseAsync(object result, JsonRpcError error = null)
        {
            var httpContext = JsonRpcContext.Current.HttpContext;
            var output = httpContext.Response.OutputStream;

            if (result is JsonRpcStreamResult streamResult)
            {
                httpContext.Response.ContentType = streamResult.ContentType;

                if (streamResult.Stream.CanSeek)
                {
                    httpContext.Response.ContentLength64 = streamResult.Stream.Length;
                }

                using (streamResult.Stream)
                {
                    await streamResult.Stream.CopyToAsync(output);
                }
            }
            else if (result is Stream stream)
            {
                httpContext.Response.ContentType = "application/octet-stream";

                if (stream.CanSeek)
                {
                    httpContext.Response.ContentLength64 = stream.Length;
                }

                using (stream)
                {
                    await stream.CopyToAsync(output);
                }
            }
            else
            {
                var response = new JsonRpcResponse
                {
                    Id = JsonRpcContext.Current?.Request?.Id,
                    JsonRpc = "2.0",
                    Result = result,
                    Error = error
                };

                httpContext.Response.ContentType = "application/json";
                var jsonResponse = JsonConvert.SerializeObject(response, SerializerSettings);

                var byteResponse = Encoding.UTF8.GetBytes(jsonResponse);
                httpContext.Response.ContentLength64 = byteResponse.Length;
                await output.WriteAsync(byteResponse, 0, byteResponse.Length);
            }

            output.Close();
        }

        public static void Stop()
        {
            Listener?.Stop();
        }

        private static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(SerializerSettings);
        }

        private static ILogger<JsonRpc> CreateLogger()
        {
            return LoggerFactory?.CreateLogger<JsonRpc>();
        }
    }
}