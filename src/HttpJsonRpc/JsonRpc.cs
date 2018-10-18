using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
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

        private static HttpListener Listener { get; set; }
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

            if (address == null) address = "http://127.0.0.1:5000/";
            if (!address.EndsWith("/")) address += "/";

            Listener = new HttpListener();
            Listener.Prefixes.Add(address);
            Listener.Start();

            CreateLogger()?.LogInformation($"Listening for JSON-RPC requests on {address}");

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
            var httpContext = JsonRpcContext.Current.HttpContext;

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

            string jsonRequest = null;
            if (httpContext.Request.QueryString.Count > 0)
            {
                jsonRequest = GetRequestFromQueryString();
            }
            else
            {
                var contentType = httpContext.Request.ContentType?.ToLowerInvariant().Split(';')[0];
                if (contentType != null)
                {
                    switch (contentType)
                    {
                        case "application/json":
                            jsonRequest = await GetRequestFromBodyAsync();
                            break;
                        case "multipart/form-data":
                            jsonRequest = await GetRequestFromFormAsync();
                            break;
                        default:
                            httpContext.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                            httpContext.Response.OutputStream.Close();
                            return;
                    }
                }
            }

            JsonRpcRequest request = null;
            try
            {
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(jsonRequest, SerializerSettings);
                if (request == null) throw new ArgumentException("Failed to parse JSON request.");
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.ParseError, e);
                return;
            }

            JsonRpcContext.Current.Request = request;

            //Commented out for now because it's a security vulnerability to list all methods. Will re-add feature when a better way is designed.
            //if (string.IsNullOrEmpty(request?.Method))
            //{
            //    var info = new JsonRpcInfo();
            //    info.Methods = Methods.ToList();
            //    await WriteResponseAsync(httpContext, JsonRpcResponse.FromResult(null, info));
            //    return;
            //}

            var method = GetMethod(request.Method);
            if (method == null)
            {
                await WriteResponseAsync(httpContext, JsonRpcError.Create(JsonRpcErrorCodes.MethodNotFound, request.Method));
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

            //Prepare the method parameters
            var parameterValues = new List<object>();
            try
            {
                var parameters = method.MethodInfo.GetParameters();
                var serializer = CreateSerializer();

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
                await HandleErrorAsync(JsonRpcErrorCodes.ParseError, e);
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
                    await WriteResponseAsync(result);

                    return;
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            }
            catch (JsonRpcUnauthorizedException e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.Unauthorized, e);
                return;
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.ExecutionError, e);
                return;
            }
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

            var method = GetMethod(jRequest["method"].ToString());

            //Move unknown values into ExtensionData
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

        private static JsonRpcMethod GetMethod(string name)
        {
            if (name == null) return null;

            name = name.ToLowerInvariant();
            if (!Methods.Contains(name)) return null;

            var method = Methods[name];

            return method;
        }

        private static async Task HandleErrorAsync(int errorCode, Exception error)
        {
            var request = JsonRpcContext.Current.Request;
            var jsonError = JsonRpcError.Create(errorCode, error?.ToString());

            var message = $"An error occured while handling the request '{request?.Method}'. JsonRpcError: {jsonError.Message} ({jsonError.Code})";
            OnError(error, message);
            await OnErrorAsync(error, message);

            try
            {
                await WriteResponseAsync(null, jsonError);
            }
            catch (Exception e)
            {
                message = "An unexpected error occured while writing response.";
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