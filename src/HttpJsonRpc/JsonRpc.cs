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
using Microsoft.AspNetCore.Http.Features;
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
                if (IsHandled(JsonRpcContext.Current)) return;
            }

            foreach (var method in OnReceivedHttpRequestAsyncMethods)
            {
                await method(context);
                if (IsHandled(JsonRpcContext.Current)) return;
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
                await InvokeAsync(context.Method.ParentClass.ReceivedRequestMethod, context.ClassInstance, new object[] { context });
                if (IsHandled(context)) return;
            }

            foreach (var method in OnReceivedRequestMethods)
            {
                method(context);
                if (IsHandled(context)) return;
            }

            foreach (var method in OnReceivedRequestAsyncMethods)
            {
                await method(context);
                if (IsHandled(context)) return;
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
                await InvokeAsync(context.Method.ParentClass.CompletedRequestMethod, context.ClassInstance, new object[] { context });
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

        private static readonly System.Threading.SemaphoreSlim HostLock = new System.Threading.SemaphoreSlim(1, 1);
        private static readonly List<Func<JsonRpcContext, Task>> FinishedHandlers = new List<Func<JsonRpcContext, Task>>();
        public static void OnRequestFinished(Action<JsonRpcContext> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            FinishedHandlers.Add(c => { handler(c); return Task.CompletedTask; });
        }
        public static void OnRequestFinished(Func<JsonRpcContext, Task> handler)
        {
            FinishedHandlers.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        }
        public static string[] ListeningAddresses => Host?.ServerFeatures
            .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses.ToArray() ?? new string[0];

        public static void Start()
        {
            HostLock.Wait();
            try
            {
                if (Host != null) throw new InvalidOperationException("The JSON-RPC host is already running.");
                if (Options.MaxBatchSize < 1) throw new InvalidOperationException("MaxBatchSize must be positive.");
                if (RpcClasses.Count == 0)
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !Options.ExcludedAssemblyPrefixes.Any(p => a.GetName().Name.StartsWith(p))))
                        RegisterMethods(assembly);
                if (Options.OpenRpc.IsEnabled)
                {
                    if (!_RpcClasses.ContainsKey("rpc")) RegisterClass(typeof(OpenRpcApi));
                    else if (_RpcClasses["rpc"].ClassType != typeof(OpenRpcApi))
                        throw new InvalidOperationException("The rpc class name is reserved for discovery.");
                }
                else if (_RpcClasses.TryGetValue("rpc", out var rpc) && rpc.ClassType == typeof(OpenRpcApi))
                    _RpcClasses = _RpcClasses.Remove("rpc");
                var host = new WebHostBuilder()
                    .ConfigureServices(s => s.AddCors())
                    .UseKestrel(ServerOptions)
                    .Configure(app =>
                    {
                        Options.ConfigureApplication?.Invoke(app);
                        if (CorsPolicy != null) app.UseCors(CorsPolicy);
                        app.Run(HandleRequestAsync);
                    }).Build();
                try { host.Start(); Host = host; }
                catch { host.Dispose(); throw; }
                CreateLogger()?.LogInformation("Listening for JSON-RPC requests");
            }
            finally { HostLock.Release(); }
        }

        public static async Task StopAsync()
        {
            await HostLock.WaitAsync();
            try
            {
                var host = Host;
                if (host == null) return;
                try { await host.StopAsync(); }
                finally { host.Dispose(); Host = null; }
            }
            finally { HostLock.Release(); }
        }

        private static bool IsHandled(JsonRpcContext context) => context.Handled || context.HttpContext.Response.HasStarted ||
            context.HttpContext.Response.StatusCode >= 400 || context.HttpContext.Response.ContentLength.HasValue;

        private static async Task HandleRequestAsync(HttpContext httpContext)
        {
            var previous = JsonRpcContext.Current;
            var context = new JsonRpcContext { HttpContext = httpContext, SerializerOptions = SerializerOptions };
            JsonRpcContext.Current = context;
            var dispatched = false;
            var errorCode = JsonRpcErrorCodes.InternalError;
            try
            {
                await OnReceivedHttpRequestAsync(httpContext);
                if (IsHandled(context)) return;
                if (httpContext.Request.Method != "GET" && httpContext.Request.Method != "POST")
                {
                    httpContext.Response.StatusCode = 405;
                    return;
                }
                if (httpContext.Request.Path.Value.EndsWith("favicon.ico", StringComparison.Ordinal)) return;
                errorCode = JsonRpcErrorCodes.ParseError;
                var node = await ReadRequestAsync(context);
                errorCode = JsonRpcErrorCodes.InvalidRequest;
                if (node is JsonArray batch)
                {
                    if (batch.Count == 0 || batch.Count > Options.MaxBatchSize)
                        throw new ArgumentException("Invalid batch size.");
                    if (httpContext.Request.QueryString.HasValue)
                        throw new ArgumentException("Batch requests cannot include query parameters.");
                    dispatched = true;
                    var responses = new JsonArray();
                    foreach (var item in batch)
                    {
                        httpContext.RequestAborted.ThrowIfCancellationRequested();
                        var itemContext = new JsonRpcContext { HttpContext = httpContext, SerializerOptions = SerializerOptions };
                        var response = await ProcessRequestAsync(itemContext, item, true);
                        if (response != null) responses.Add(response);
                        if (IsHandled(itemContext)) break;
                    }
                    if (!httpContext.Response.HasStarted && httpContext.Response.StatusCode < 400)
                    {
                        if (responses.Count == 0) httpContext.Response.StatusCode = 204;
                        else await WriteJsonAsync(httpContext, responses);
                    }
                }
                else
                {
                    if (node is JsonObject request && httpContext.Request.QueryString.HasValue)
                        PopulateRequestFromQueryString(context, request);
                    dispatched = true;
                    await ProcessRequestAsync(context, node, false);
                }
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested) { }
            catch (Exception e)
            {
                context.Exception = e;
                if (e is JsonRpcUnauthorizedException) errorCode = JsonRpcErrorCodes.Unauthorized;
                var envelope = await CreateErrorAsync(context, errorCode, e);
                if (!httpContext.Response.HasStarted && !context.Handled)
                    await WriteJsonAsync(httpContext, envelope);
            }
            finally
            {
                if (!dispatched) await FinishRequestAsync(context);
                JsonRpcContext.Current = previous;
            }
        }

        private static async Task<JsonNode> ReadRequestAsync(JsonRpcContext context)
        {
            var request = context.HttpContext.Request;
            var type = request.ContentType?.Split(';')[0].Trim();
            if (string.Equals(type, "application/json", StringComparison.OrdinalIgnoreCase))
                return await JsonSerializer.DeserializeAsync<JsonNode>(request.Body, cancellationToken: context.CancellationToken);
            if (string.Equals(type, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                var form = await request.ReadFormAsync(context.CancellationToken);
                if (form["request"].Count != 1) throw new JsonException("Expected one request form field.");
                return JsonNode.Parse(form["request"][0]);
            }
            return new JsonObject(); // Legacy query-based RPC and binary uploads.
        }

        private static void PopulateRequestFromQueryString(JsonRpcContext context, JsonObject request)
        {
            var parameters = request["params"] as JsonObject;
            if (request["params"] == null) request["params"] = parameters = new JsonObject();
            foreach (var pair in context.HttpContext.Request.Query)
            {
                if (pair.Key == "jsonrpc" || pair.Key == "id" || pair.Key == "method" || pair.Key == "version")
                    request[pair.Key] = pair.Value.FirstOrDefault();
                else if (parameters != null) parameters[pair.Key] = pair.Value.FirstOrDefault();
                else request[pair.Key] = pair.Value.FirstOrDefault();
            }
            var method = GetMethod(request["method"]?.ToString(), request["version"]?.ToString() ?? "");
            if (parameters == null || method == null) return;
            foreach (var pair in parameters.ToArray())
            {
                if (method.Parameters.Any(p => p.Name == pair.Key)) continue;
                // Preserve legacy extension data, but never overwrite the protocol envelope.
                if (pair.Key == "method" || pair.Key == "id" || pair.Key == "jsonrpc" || pair.Key == "params" || pair.Key == "version") continue;
                request[pair.Key] = pair.Value?.DeepClone();
                parameters.Remove(pair.Key);
            }
        }

        private static async Task<JsonNode> ProcessRequestAsync(JsonRpcContext context, JsonNode node, bool batch)
        {
            var previous = JsonRpcContext.Current;
            JsonRpcContext.Current = context;
            var code = JsonRpcErrorCodes.InvalidRequest;
            try
            {
                context.RequestJson = node as JsonObject ?? throw new ArgumentException("Expected a request object.");
                ValidateRequest(context.RequestJson, Options.StrictProtocol);
                context.Request = context.RequestJson.Deserialize<JsonRpcRequest>(context.SerializerOptions);
                context.IsNotification = Options.StrictProtocol && !context.RequestJson.ContainsKey("id");
                code = JsonRpcErrorCodes.MethodNotFound;
                context.Method = GetMethod(context.Request.Method, context.Request.Version ?? "");
                if (context.Method == null) throw new ArgumentException("Method not found.");
                code = JsonRpcErrorCodes.InternalError;
                if (!context.Method.MethodInfo.IsStatic)
                {
                    context.OwnsClassInstance = !ServiceLocator.IsLocationProviderSet;
                    context.ClassInstance = context.OwnsClassInstance ? Activator.CreateInstance(context.Method.MethodInfo.ReflectedType) :
                        ServiceLocator.Current.GetInstance(context.Method.MethodInfo.ReflectedType);
                }
                await OnReceivedRequestAsync(context);
                if (IsHandled(context)) return null;
                code = JsonRpcErrorCodes.InvalidParams;
                await BindParametersAsync(context);
                code = JsonRpcErrorCodes.InternalError;
                context.Result = await InvokeAsync(context.Method.MethodInfo, context.ClassInstance, context.RequestParameters.ToArray());
                JsonNode envelope = null;
                if (context.IsNotification)
                {
                    DisposeResult(context.Result);
                    if (!batch) context.HttpContext.Response.StatusCode = 204;
                }
                else if (batch)
                {
                    if (context.Result is Stream || context.Result is JsonRpcStreamResult)
                    {
                        DisposeResult(context.Result);
                        throw new InvalidOperationException("Streaming results are not supported inside a batch.");
                    }
                    envelope = CreateEnvelope(context, null);
                }
                else await WriteResultAsync(context);
                await OnCompletedRequestAsync(context);
                return envelope;
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                e = Unwrap(e);
                context.Exception = e;
                if (e is JsonRpcUnauthorizedException) code = JsonRpcErrorCodes.Unauthorized;
                var envelope = await CreateErrorAsync(context, code, e);
                if (context.IsNotification)
                {
                    if (!batch && !context.HttpContext.Response.HasStarted) context.HttpContext.Response.StatusCode = 204;
                    return null;
                }
                if (batch) return envelope;
                if (!context.HttpContext.Response.HasStarted) await WriteJsonAsync(context.HttpContext, envelope);
                return null;
            }
            finally
            {
                await FinishRequestAsync(context);
                JsonRpcContext.Current = previous;
            }
        }

        internal static void ValidateRequest(JsonObject request, bool strict)
        {
            if (!strict) return;
            if (request["jsonrpc"] is not JsonValue version || !version.TryGetValue<string>(out var v) || v != "2.0")
                throw new ArgumentException("Invalid protocol version.");
            if (request["method"] is not JsonValue method || !method.TryGetValue<string>(out var name) || string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid method.");
            if (request.ContainsKey("params") && !(request["params"] is JsonObject) && !(request["params"] is JsonArray))
                throw new ArgumentException("Invalid params structure.");
            if (request["id"] != null)
            {
                var kind = JsonSerializer.SerializeToElement(request["id"]).ValueKind;
                if (kind != JsonValueKind.String && kind != JsonValueKind.Number) throw new ArgumentException("Invalid request id.");
            }
        }

        internal static async Task BindParametersAsync(JsonRpcContext context)
        {
            var parameters = context.Method.MethodInfo.GetParameters();
            var values = context.Request.Params;
            if (context.Method.ParamsType != null)
            {
                if (!values.HasValue || values.Value.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Expected an object parameter.");
                context.RequestParameters.Add(values.Value.Deserialize(context.Method.ParamsType, context.SerializerOptions));
                return;
            }
            if (values.HasValue && values.Value.ValueKind != JsonValueKind.Array && values.Value.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Expected named or positional parameters.");
            var position = 0;
            foreach (var parameter in parameters)
            {
                if (parameter.ParameterType == typeof(System.Threading.CancellationToken))
                { context.RequestParameters.Add(context.CancellationToken); continue; }
                var attribute = parameter.GetCustomAttribute<JsonRpcParameterAttribute>();
                if (attribute?.Ignore == true)
                {
                    if (!parameter.IsOptional) throw new ArgumentException("Ignored parameters must be optional.");
                    context.RequestParameters.Add(Type.Missing); continue;
                }
                var value = default(JsonElement);
                if (values.HasValue)
                {
                    if (values.Value.ValueKind == JsonValueKind.Array)
                    { if (position < values.Value.GetArrayLength()) value = values.Value[position]; }
                    else values.Value.TryGetProperty(attribute?.Name ?? parameter.Name, out value);
                }
                position++;
                if (value.ValueKind == JsonValueKind.Undefined)
                {
                    if (!parameter.IsOptional) throw new ArgumentException($"Missing parameter '{parameter.Name}'.");
                    context.RequestParameters.Add(Type.Missing);
                }
                else if (context.Method.ParentClass.DeserializeParameterMethod != null)
                    context.RequestParameters.Add(await InvokeAsync(context.Method.ParentClass.DeserializeParameterMethod, context.ClassInstance,
                        new object[] { value, parameter, context.SerializerOptions, context }));
                else context.RequestParameters.Add(value.Deserialize(parameter.ParameterType, context.SerializerOptions));
            }
            if (Options.StrictProtocol && values.HasValue)
            {
                if (values.Value.ValueKind == JsonValueKind.Array && values.Value.GetArrayLength() > position)
                    throw new ArgumentException("Too many parameters.");
                if (values.Value.ValueKind == JsonValueKind.Object && values.Value.EnumerateObject().Any(p => !context.Method.Parameters.Any(m => m.Name == p.Name)))
                    throw new ArgumentException("Unknown parameter.");
            }
        }

        internal static async Task<object> InvokeAsync(MethodInfo method, object instance, object[] args)
        {
            object value;
            try { value = method.Invoke(instance, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw(); throw; }
            if (value is ValueTask vt) { await vt; return null; }
            if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                value = value.GetType().GetMethod("AsTask").Invoke(value, null);
            if (value is Task task)
            {
                await task;
                var declared = method.ReturnType;
                return declared.IsGenericType ? task.GetType().GetProperty("Result")?.GetValue(task) : null;
            }
            return value;
        }
        private static Exception Unwrap(Exception e) => e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;

        private static async Task<JsonNode> CreateErrorAsync(JsonRpcContext context, int code, Exception error)
        {
            var message = $"An error occurred while handling '{context.Request?.Method}'.";
            OnError(error, message);
            await OnErrorAsync(error, message);
            try
            {
                var jsonError = Options.ErrorFactory.CreateError(new JsonRpcErrorFactory.CreateErrorArgs
                { ErrorCode = code, Exception = error, Context = context, Options = Options });
                return CreateEnvelope(context, jsonError);
            }
            catch (Exception factoryError)
            {
                OnError(factoryError, "Error response customization failed.");
                return new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = JsonSerializer.SerializeToNode(context.Request?.Id),
                    ["error"] = new JsonObject { ["code"] = JsonRpcErrorCodes.InternalError, ["message"] = "Internal error" }
                };
            }
        }

        internal static JsonObject CreateEnvelope(JsonRpcContext context, JsonRpcError error)
        {
            var options = context.Method?.ParentClass.GetSerializerOptionsMethod?.Invoke(context.ClassInstance,
                new object[] { context, context.SerializerOptions }) as JsonSerializerOptions ?? context.SerializerOptions;
            var envelope = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JsonSerializer.SerializeToNode(context.Request?.Id, options)
            };
            if (error == null) envelope["result"] = JsonSerializer.SerializeToNode(context.Result, context.Result?.GetType() ?? typeof(object), options);
            else
            {
                var e = new JsonObject { ["code"] = error.Code, ["message"] = error.Message };
                if (error.Data != null) e["data"] = JsonSerializer.SerializeToNode(error.Data, options);
                envelope["error"] = e;
            }
            return envelope;
        }

        private static Task WriteJsonAsync(HttpContext http, JsonNode node)
        {
            http.Response.ContentType = "application/json";
            return JsonSerializer.SerializeAsync(http.Response.Body, node, cancellationToken: http.RequestAborted);
        }
        private static async Task WriteResultAsync(JsonRpcContext context)
        {
            var stream = context.Result as Stream ?? (context.Result as JsonRpcStreamResult)?.Stream;
            if (stream == null) { await WriteJsonAsync(context.HttpContext, CreateEnvelope(context, null)); return; }
            using (stream)
            {
                context.HttpContext.Response.ContentType = (context.Result as JsonRpcStreamResult)?.ContentType ?? "application/octet-stream";
                if (stream.CanSeek) context.HttpContext.Response.ContentLength = Math.Max(0, stream.Length - stream.Position);
                await stream.CopyToAsync(context.HttpContext.Response.Body, 81920, context.CancellationToken);
            }
        }
        private static void DisposeResult(object result)
        {
            (result as Stream ?? (result as JsonRpcStreamResult)?.Stream)?.Dispose();
        }
        private static async Task FinishRequestAsync(JsonRpcContext context)
        {
            foreach (var handler in FinishedHandlers)
            {
                try { await handler(context); }
                catch (Exception e) { OnError(e, "Request cleanup failed."); }
            }
            if (context.OwnsClassInstance)
            {
                try
                {
                    if (context.ClassInstance is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
                    else (context.ClassInstance as IDisposable)?.Dispose();
                }
                catch (Exception e) { OnError(e, "RPC instance disposal failed."); }
            }
        }
        private static ILogger<JsonRpc> CreateLogger() => LoggerFactory?.CreateLogger<JsonRpc>();
        private static JsonRpcMethod GetMethod(string fullMethodName, string version)
        {
            if (string.IsNullOrWhiteSpace(fullMethodName)) return null;
            var parts = fullMethodName.Split('.');
            if (parts.Length != 2) return null;
            var classKey = string.IsNullOrWhiteSpace(version) ? parts[0].ToLowerInvariant() : $"{version.ToLowerInvariant()}:{parts[0].ToLowerInvariant()}";
            if (!_RpcClasses.TryGetValue(classKey, out var rpcClass)) return null;
            if (Options.StrictProtocol && parts[0] != rpcClass.Name) return null;
            if (!rpcClass.Methods.TryGetValue(parts[1].ToLowerInvariant(), out var method)) return null;
            return Options.StrictProtocol && parts[1] != method.Name ? null : method;
        }
    }
}
