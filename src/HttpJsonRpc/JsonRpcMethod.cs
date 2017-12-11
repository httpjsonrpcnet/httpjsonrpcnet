using System.Collections.Generic;

namespace HttpJsonRpc
{
    public class JsonRpcMethod
    {
        public string Name { get; set; }
        public List<JsonRpcParameter> Parameters { get; } = new List<JsonRpcParameter>();
    }
}