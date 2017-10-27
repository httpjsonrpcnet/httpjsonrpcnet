using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpJsonRpcNet
{
    public static class HttpJsonRpc
    {
        public class Request
        {
            public string JsonRpc { get; set; }
            public object Id { get; set; }
            public JToken Params { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JToken> ExtensionData { get; set; }
        }

        public class Response
        {
            public string JsonRpc { get; set; }
            public object Id { get; set; }
            public object Result { get; set; }
        }

        public interface IProcedure
        {
            string Name { get; }
            Type ParamsType { get; }
            
            Task<object> Invoke(JToken @params);
        }

        public class Procedure<TParams, TResult> : IProcedure
        {
            public string Name { get; }
            public Type ParamsType => typeof(TParams);
            public Func<TParams, Task<TResult>> Func { get; }

            public Procedure(string name, Func<TParams, Task<TResult>> func)
            {
                Name = name;
                Func = func;
            }

            public async Task<object> Invoke(JToken @params)
            {
                var result = await Func(@params.ToObject<TParams>());
                return result;
            }
        }

        private static readonly AsyncLocal<Request> _CurrentRequest = new AsyncLocal<Request>();
        public static Request CurrentRequest
        {
            get => _CurrentRequest.Value;
            set => _CurrentRequest.Value = value;
        }

        private static HttpListener Listener { get; set; }
        private static Dictionary<string, IProcedure> Procedures { get; } = new Dictionary<string, IProcedure>();

        public static void AddProcedure<TParams, TResult>(string name, Func<TParams, Task<TResult>> func)
        {
            name = name.ToLowerInvariant();
            var procedure = new Procedure<TParams, TResult>(name, func);
            Procedures.Add(name, procedure);
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
            var procedureName = httpContext.Request.Url.AbsolutePath
                .Remove(0, 1)
                .ToLowerInvariant();

            string requestJson;
            using (var reader = new StreamReader(httpContext.Request.InputStream))
            {
                requestJson = await reader.ReadToEndAsync();
            }

            var request = JsonConvert.DeserializeObject<Request>(requestJson);
            CurrentRequest = request;

            var procedure = Procedures[procedureName];
            var result = await procedure.Invoke(request.Params);
            var response = new Response
            {
                Id = request.Id,
                JsonRpc = "2.0",
                Result = result
            };

            var jsonResponse = JsonConvert.SerializeObject(response);

            httpContext.Response.ContentType = "application/json";
            var byteResponse = Encoding.UTF8.GetBytes(jsonResponse);
            await httpContext.Response.OutputStream.WriteAsync(byteResponse, 0, byteResponse.Length);
            httpContext.Response.OutputStream.Close();
        }

        public static void Stop()
        {
            Listener?.Stop();
        }
    }
}