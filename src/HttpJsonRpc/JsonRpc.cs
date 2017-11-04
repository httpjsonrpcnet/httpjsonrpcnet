using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HttpJsonRpc
{
    public static class JsonRpc
    {
        private static readonly AsyncLocal<Request> _CurrentRequest = new AsyncLocal<Request>();
        public static Request CurrentRequest
        {
            get => _CurrentRequest.Value;
            set => _CurrentRequest.Value = value;
        }

        private static HttpListener Listener { get; set; }
        private static Dictionary<string, MethodInfo> Procedures { get; } = new Dictionary<string, MethodInfo>();
        public static JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        private static List<Func<Request, Task>> OnReceivedRequestFuncs { get; } = new List<Func<Request, Task>>();

        public static void OnReceivedRequest(Func<Request, Task> func)
        {
            OnReceivedRequestFuncs.Add(func);
        }

        public static void RegisterProcedures(Assembly fromAssembly)
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

                        Procedures.Add(name, m);
                    }
                }
            }
        }

        public static async void Start(string address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (!address.EndsWith("/")) address += "/";

            Listener = new HttpListener();
            Listener.Prefixes.Add(address);
            Listener.Start();

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
                return;
            }

            if (httpContext.Request.ContentType == null || !new[] { "application/json" }.Contains(httpContext.Request.ContentType, StringComparer.InvariantCultureIgnoreCase))
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                return;
            }

            Request request;
            try
            {
                string requestJson;
                using (var reader = new StreamReader(httpContext.Request.InputStream))
                {
                    requestJson = await reader.ReadToEndAsync();
                }

                request = JsonConvert.DeserializeObject<Request>(requestJson);
                CurrentRequest = request;
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, Response.FromError(ErrorCodes.ParseError, null, e));
                return;
            }

            try
            {
                foreach (var f in OnReceivedRequestFuncs)
                {
                    await f(request);
                }
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, Response.FromError(ErrorCodes.InternalError, request.Id, e));
                return;
            }

            var method = request.Method?.ToLowerInvariant() ?? string.Empty;
            if (!Procedures.TryGetValue(method, out var procedure))
            {
                await WriteResponseAsync(httpContext, Response.FromError(ErrorCodes.MethodNotFound, request.Id));
                return;
            }

            var parametersType = procedure.GetParameters().Single().ParameterType;

            object parameters;
            try
            {
                parameters = request.Params?.ToObject(parametersType);
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, Response.FromError(ErrorCodes.ParseError, request.Id, e));
                return;
            }

            try
            {
                var resultTask = (Task)procedure.Invoke(null, new[] { parameters });
                await resultTask;
                var result = resultTask.GetType().GetProperty("Result").GetValue(resultTask);

                var response = new Response
                {
                    Id = request.Id,
                    JsonRpc = "2.0",
                    Result = result
                };

                await WriteResponseAsync(httpContext, response);
            }
            catch (Exception e)
            {
                await WriteResponseAsync(httpContext, Response.FromError(ErrorCodes.ExecutionError, request.Id, e));
                return;
            }
        }

        private static async Task WriteResponseAsync(HttpListenerContext context, Response response)
        {
            context.Response.ContentType = "application/json";
            var jsonResponse = JsonConvert.SerializeObject(response);
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