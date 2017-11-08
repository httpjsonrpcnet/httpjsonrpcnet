using System.Collections.Generic;
using System.Threading;

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

        public Request Request { get; }
        public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();

        public JsonRpcContext(Request request)
        {
            Request = request;
        }
    }
}