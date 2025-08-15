using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace HttpJsonRpc
{
    public class JsonRpcContext
    {
        private static readonly AsyncLocal<JsonRpcContext> _Current = new AsyncLocal<JsonRpcContext>();
        public static JsonRpcContext Current
        {
            get => _Current.Value;
            set => _Current.Value = value;
        }

        public HttpContext HttpContext { get; set; }
        public string RequestJson { get; set; }
        public JsonRpcRequest Request { get; set; }
        public JsonRpcMethod Method { get; set; }
        public object ClassInstance { get; set; }
        public List<object> RequestParameters { get; set; } = new List<object>();
        public object Result { get; set; }
        public JsonSerializerOptions SerializerOptions { get; set; }

        public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();
    }
}