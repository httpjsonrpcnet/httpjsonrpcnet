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
using Newtonsoft.Json.Serialization;

namespace HttpJsonRpc
{
    public static class JsonRpc
    {
        private static HttpListener Listener { get; set; }
        private static Dictionary<string, MethodInfo> Methods { get; } = new Dictionary<string, MethodInfo>();
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
                foreach (var m in t.DeclaredMethods)
                {
                    var a = m.GetCustomAttribute<JsonRpcMethodAttribute>();
                    if (a != null)
                    {
                        var name = a.Name ?? $"{m.DeclaringType.Name}.{m.Name}";
                        var asyncIndex = name.LastIndexOf("Async", StringComparison.Ordinal);
                        if (asyncIndex > -1)
                        {
                            name = name.Remove(asyncIndex);
                        }

                        name = name.ToLowerInvariant();

                        Methods.Add(name, m);
                    }
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
                var httpContext = await Listener.GetContextAsync();
                HandleRequest(httpContext);
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

            if (!httpContext.Request.ContentType?.ToLowerInvariant().Split(';')?.Contains("application/json") ?? false)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                httpContext.Response.OutputStream.Close();
                return;
            }

            JsonRpcRequest request;
            try
            {
                string requestJson;
                using (var reader = new StreamReader(httpContext.Request.InputStream))
                {
                    requestJson = await reader.ReadToEndAsync();
                }

                request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson);
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.ParseError, null, e));
                return;
            }

            var jsonRpcContext = new JsonRpcContext(request);
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

            var methodName = request.Method?.ToLowerInvariant() ?? string.Empty;
            if (!Methods.TryGetValue(methodName, out var method))
            {
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.MethodNotFound, request.Id, method));
                return;
            }

            var parameterValues = new List<object>();
            try
            {
                var parameters = method.GetParameters();

                foreach (var parameter in parameters)
                {
                    var parameterAttribute = parameter.GetCustomAttribute<JsonRpcParameterAttribute>();
                    if (parameterAttribute?.Ignore == true)
                    {
                        parameterValues.Add(Type.Missing);
                        continue;
                    }

                    var parameterName = parameterAttribute?.Name ?? parameter.Name;
                    var value = request.Params?[parameterName]?.ToObject(parameter.ParameterType) ?? Type.Missing;
                    parameterValues.Add(value);
                }
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, JsonRpcResponse.FromError(JsonRpcErrorCodes.ParseError, request.Id, e));
                return;
            }

            try
            {
                try
                {
                    var resultTask = (Task)method.Invoke(null, parameterValues.ToArray());
                    await resultTask;
                    var result = resultTask.GetType().GetProperty("Result").GetValue(resultTask);

                    var response = new JsonRpcResponse
                    {
                        Id = request.Id,
                        JsonRpc = "2.0",
                        Result = result
                    };

                    await WriteResponseAsync(httpContext, response);
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

        private static async Task WriteResponseAsync(HttpListenerContext context, JsonRpcResponse jsonRpcResponse)
        {
            context.Response.ContentType = "application/json";
            var jsonResponse = JsonConvert.SerializeObject(jsonRpcResponse, SerializerSettings);
            var byteResponse = Encoding.UTF8.GetBytes(jsonResponse);
            await context.Response.OutputStream.WriteAsync(byteResponse, 0, byteResponse.Length);
            context.Response.OutputStream.Close();
        }

        public static void Stop()
        {
            Listener?.Stop();
        }
    }
}