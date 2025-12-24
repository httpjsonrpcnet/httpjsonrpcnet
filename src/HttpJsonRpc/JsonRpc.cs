using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommonServiceLocator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HttpJsonRpc
{
    public class JsonRpc
    {
        private static IWebHost Host { get; set; }

        public static JsonRpcOptions Options { get; } = new JsonRpcOptions();
        public static ILoggerFactory LoggerFactory { get => Options.LoggerFactory; set => Options.LoggerFactory = value; }
        public static JsonSerializerOptions SerializerOptions { get => Options.SerializerOptions; set => Options.SerializerOptions = value; }
        public static Action<KestrelServerOptions> ServerOptions { get => Options.ServerOptions; set => Options.ServerOptions = value; }
        public static Action<CorsPolicyBuilder> CorsPolicy { get => Options.CorsPolicy; set => Options.CorsPolicy = value; }

        private static ImmutableDictionary<string, JsonRpcClass> _RpcClasses = ImmutableDictionary<string, JsonRpcClass>.Empty;
        public static ImmutableDictionary<string, JsonRpcClass> RpcClasses => _RpcClasses;

        private static List<Func<HttpContext, Task>> OnReceivedHttpRequestAsyncMethods { get; } = new List<Func<HttpContext, Task>>();
        private static List<Func<JsonRpcContext, Task>> OnReceivedRequestAsyncMethods { get; } = new List<Func<JsonRpcContext, Task>>();
        private static List<Func<JsonRpcContext, Task>> OnCompletedRequestAsyncMethods { get; } = new List<Func<JsonRpcContext, Task>>();
        private static List<Func<Exception, Task>> OnErrorAsyncMethods { get; } = new List<Func<Exception, Task>>();

        private static List<Action<HttpContext>> OnReceivedHttpRequestMethods { get; } = new List<Action<HttpContext>>();
        private static List<Action<JsonRpcContext>> OnReceivedRequestMethods { get; } = new List<Action<JsonRpcContext>>();
        private static List<Action<JsonRpcContext>> OnCompletedRequestMethods { get; } = new List<Action<JsonRpcContext>>();
        private static List<Action<Exception>> OnErrorMethods { get; } = new List<Action<Exception>>();

        private JsonRpc()
        {
        }

        public static void OnReceivedHttpRequest(Action<HttpContext> method)
        {
            OnReceivedHttpRequestMethods.Add(method);
        }

        public static void OnReceivedHttpRequest(Func<HttpContext, Task> method)
        {
            OnReceivedHttpRequestAsyncMethods.Add(method);
        }

        private static async Task OnReceivedHttpRequestAsync(HttpContext context)
        {
            foreach (var method in OnReceivedHttpRequestMethods)
            {
                method(context);
            }

            foreach (var method in OnReceivedHttpRequestAsyncMethods)
            {
                await method(context);
            }
        }

        public static void OnReceivedRequest(Action<JsonRpcContext> method)
        {
            OnReceivedRequestMethods.Add(method);
        }

        public static void OnReceivedRequest(Func<JsonRpcContext, Task> method)
        {
            OnReceivedRequestAsyncMethods.Add(method);
        }

        private static async Task OnReceivedRequestAsync(JsonRpcContext context)
        {
            if (context.Method.ParentClass.ReceivedRequestMethod != null)
            {
                var methodTask = (Task)context.Method.ParentClass.ReceivedRequestMethod.Invoke(context.ClassInstance, new object[] { context });
                if (methodTask != null) await methodTask;
            }

            foreach (var method in OnReceivedRequestMethods)
            {
                method(context);
            }

            foreach (var method in OnReceivedRequestAsyncMethods)
            {
                await method(context);
            }
        }

        public static void OnCompletedRequest(Action<JsonRpcContext> method)
        {
            OnCompletedRequestMethods.Add(method);
        }

        public static void OnCompletedRequest(Func<JsonRpcContext, Task> method)
        {
            OnCompletedRequestAsyncMethods.Add(method);
        }

        private static async Task OnCompletedRequestAsync(JsonRpcContext context)
        {
            if (context.Method.ParentClass.CompletedRequestMethod != null)
            {
                var methodTask = (Task)context.Method.ParentClass.CompletedRequestMethod.Invoke(context.ClassInstance, new object[] { context });
                if (methodTask != null) await methodTask;
            }

            foreach (var method in OnCompletedRequestMethods)
            {
                method(context);
            }

            foreach (var method in OnCompletedRequestAsyncMethods)
            {
                await method(context);
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

        public static void RegisterClass(Type type)
        {
            var rpcClass = new JsonRpcClass(type);
            _RpcClasses = _RpcClasses.Add(rpcClass.Key, rpcClass);
        }

        public static void RegisterMethods(Assembly fromAssembly)
        {
            if (fromAssembly == null) throw new ArgumentNullException(nameof(fromAssembly));

            var classTypes = fromAssembly.DefinedTypes.Where(i => i.IsDefined(typeof(JsonRpcClassAttribute), true)).ToArray();
            var classesBuilder = _RpcClasses.ToBuilder();
            foreach (var t in classTypes)
            {
                var rpcClass = new JsonRpcClass(t);
                classesBuilder.Add(rpcClass.Key, rpcClass);
            }
            _RpcClasses = classesBuilder.ToImmutable();
        }

        public static void Start()
        {
            if (RpcClasses.Count == 0)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !Options.ExcludedAssemblyPrefixes.Any(prefix => a.GetName().Name.StartsWith(prefix)));

                foreach (var assembly in assemblies)
                {
                    RegisterMethods(assembly);
                }
            }

            if (Options.OpenRpc.IsEnabled)
            {
                RegisterClass(typeof(OpenRpcApi));
            }

            Host = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddCors();
                })
                .UseKestrel(ServerOptions)
                .Configure(app =>
                {
                    if (CorsPolicy != null)
                    {
                        app.UseCors(CorsPolicy);
                    }

                    app.Run(HandleRequestAsync);
                })
                .Build();

            Host.Start();

            CreateLogger()?.LogInformation($"Listening for JSON-RPC requests");
        }

        private static async Task HandleRequestAsync(HttpContext httpContext)
        {
            var context = new JsonRpcContext { HttpContext = httpContext, SerializerOptions = SerializerOptions };
            JsonRpcContext.Current = context;

            try
            {
                await OnReceivedHttpRequestAsync(httpContext);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }

            try
            {
                if ((httpContext.Response.ContentLength ?? 0) != 0) return;

                if (!new[] { "GET", "POST" }.Contains(httpContext.Request.Method, StringComparer.InvariantCultureIgnoreCase))
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                if (httpContext.Request.Path.Value.EndsWith("favicon.ico"))
                {
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
                await SetContextRequestJsonAsync(context);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InvalidRequest, e);
                return;
            }

            try
            {
                SetContextRequest(context);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.ParseError, e);
                return;
            }

            try
            {
                SetContextMethod(context);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.MethodNotFound, e);
                return;
            }

            try
            {
                SetContextClassInstance(context);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }

            try
            {
                await OnReceivedRequestAsync(context);
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

            if ((httpContext.Response.ContentLength ?? 0) != 0) return;

            try
            {
                await SetContextRequestParametersAsync(context);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.ParseError, e);
                return;
            }

            try
            {
                await ExecuteMethodAsync(context);
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

            try
            {
                await OnCompletedRequestAsync(context);
            }
            catch (Exception e)
            {
                await HandleErrorAsync(JsonRpcErrorCodes.InternalError, e);
                return;
            }
        }

        private static async Task SetContextRequestJsonAsync(JsonRpcContext context)
        {
            string requestJson = null;
            if (context.HttpContext.Request.QueryString.HasValue)
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
            using (var reader = new StreamReader(httpContext.Request.Body))
            {
                jsonRequest = await reader.ReadToEndAsync();
            }

            return jsonRequest;
        }

        private static string GetRequestFromQueryString()
        {
            var httpContext = JsonRpcContext.Current.HttpContext;

            var jRequest = new JsonObject();
            var parameters = new JsonObject();
            jRequest["params"] = parameters;

            var queryString = httpContext.Request.Query;
            foreach (var key in queryString.Keys)
            {
                if (key == null) continue; ;
                var value = queryString[key];

                if (key == "jsonrpc" || key == "id" || key == "method" || key == "version")
                {
                    jRequest[key] = value.FirstOrDefault();
                    continue;
                }

                parameters[key] = value.FirstOrDefault();
            }

            //Move unknown values into ExtensionData
            var methodName = jRequest["method"]?.ToString();
            var version = jRequest["version"]?.ToString() ?? "";
            var rpcMethod = GetMethod(methodName, version);

            if (rpcMethod != null)
            {
                var extensionData = new Dictionary<string, string>();
                foreach (var parameter in parameters)
                {
                    if (!rpcMethod.Parameters.Any(p => p.Name == parameter.Key))
                    {
                        extensionData[parameter.Key] = parameter.Value.ToString();
                    }
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

            using (var reader = new StreamReader(httpContext.Request.Body))
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

        private static void SetContextRequest(JsonRpcContext context)
        {
            context.Request = JsonSerializer.Deserialize<JsonRpcRequest>(context.RequestJson, SerializerOptions);
            if (context.Request == null) throw new ArgumentException("Failed to parse JSON request");
        }

        private static void SetContextMethod(JsonRpcContext context)
        {
            var methodName = context.Request.Method;
            var version = context.Request.Version ?? "";
            var rpcMethod = GetMethod(methodName, version);

            if (rpcMethod == null) throw new ArgumentException($"Invalid method '{context.Request.Method}'");

            context.Method = rpcMethod;
        }

        private static void SetContextClassInstance(JsonRpcContext context)
        {
            var methodInfo = context.Method.MethodInfo;

            if (!methodInfo.IsStatic)
            {
                context.ClassInstance = ServiceLocator.IsLocationProviderSet ? ServiceLocator.Current.GetInstance(methodInfo.ReflectedType) : Activator.CreateInstance(methodInfo.ReflectedType);
            }
        }

        private static async Task SetContextRequestParametersAsync(JsonRpcContext context)
        {
            var rpcMethod = context.Method;

            if (rpcMethod.ParamsType != null)
            {
                if (context.Request.Params != null)
                {
                    var value = context.Request.Params.Value.Deserialize(rpcMethod.ParamsType, SerializerOptions);
                    context.RequestParameters.Add(value);
                }

                return;
            }

            var parameterInfos = rpcMethod.MethodInfo.GetParameters();
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                var parameter = parameterInfos[i];
                var parameterAttribute = parameter.GetCustomAttribute<JsonRpcParameterAttribute>();
                if (parameterAttribute?.Ignore == true)
                {
                    context.RequestParameters.Add(Type.Missing);
                    continue;
                }

                var parameterName = parameterAttribute?.Name ?? parameter.Name;
                object value = Type.Missing;

                if (context.Request.Params != null)
                {
                    var requestParams = context.Request.Params.Value;
                    JsonElement valueElement = default;

                    if (requestParams.ValueKind == JsonValueKind.Array)
                    {
                        if (i < requestParams.GetArrayLength())
                        {
                            valueElement = requestParams[i];
                        }
                    }
                    else
                    {
                        requestParams.TryGetProperty(parameterName, out valueElement);
                    }

                    if (valueElement.ValueKind != JsonValueKind.Undefined)
                    {
                        if (context.Method.ParentClass.DeserializeParameterMethod != null)
                        {
                            //Expected signature: Task<object> DeserializeParameterAsync(JsonElement value, ParameterInfo parameter, JsonSerializerOptions serializerOptions, JsonRpcContext context)
                            value = await (Task<object>)context.Method.ParentClass.DeserializeParameterMethod.Invoke(context.ClassInstance, new object[] { valueElement, parameter, SerializerOptions, context });
                        }
                        else
                        {
                            value = valueElement.Deserialize(parameter.ParameterType, SerializerOptions);
                        }
                    }
                }

                context.RequestParameters.Add(value);
            }
        }

        private static async Task ExecuteMethodAsync(JsonRpcContext context)
        {
            try
            {
                var methodTask = (Task)context.Method.MethodInfo.Invoke(context.ClassInstance, context.RequestParameters.ToArray());
                await methodTask;
                context.Result = methodTask.GetType().GetProperty("Result")?.GetValue(methodTask);
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        private static async Task HandleErrorAsync(int errorCode, Exception error)
        {
            var request = JsonRpcContext.Current.Request;
            var jsonError = Options.ErrorFactory.CreateError(new JsonRpcErrorFactory.CreateErrorArgs
            {
                ErrorCode = errorCode,
                Exception = error,
                Context = JsonRpcContext.Current,
                Options = Options
            });

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
            var context = JsonRpcContext.Current;
            var httpContext = context.HttpContext;
            var output = httpContext.Response.Body;

            if (result is JsonRpcStreamResult streamResult)
            {
                httpContext.Response.ContentType = streamResult.ContentType;

                if (streamResult.Stream.CanSeek)
                {
                    httpContext.Response.ContentLength = streamResult.Stream.Length;
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
                    httpContext.Response.ContentLength = stream.Length;
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

                var serializerOptions = (JsonSerializerOptions)context.Method.ParentClass.GetSerializerOptionsMethod
                    ?.Invoke(context.ClassInstance, new object[] { context, SerializerOptions })
                    ?? SerializerOptions;

                await JsonSerializer.SerializeAsync(output, response, serializerOptions);

                if (output.CanSeek)
                {
                    httpContext.Response.ContentLength = output.Length;
                }
            }

            output.Close();
        }

        public static async Task StopAsync()
        {
            if (Host == null)
            {
                return;
            }

            await Host.StopAsync();
        }

        private static ILogger<JsonRpc> CreateLogger()
        {
            return LoggerFactory?.CreateLogger<JsonRpc>();
        }

        private static JsonRpcMethod GetMethod(string fullMethodName, string version)
        {
            if (string.IsNullOrWhiteSpace(fullMethodName)) return null;

            var parts = fullMethodName.Split('.');
            if (parts.Length != 2) return null;

            var className = parts[0].ToLowerInvariant();
            var classKey = string.IsNullOrWhiteSpace(version) ? className : $"{version.ToLowerInvariant()}:{className}";
            var methodName = parts[1].ToLowerInvariant();

            if (RpcClasses.TryGetValue(classKey, out var rpcClass) && rpcClass.Methods.TryGetValue(methodName, out var rpcMethod)) return rpcMethod;

            return null;
        }
    }
}