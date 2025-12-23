using System.Collections.Generic;

namespace HttpJsonRpc
{
    public class OpenRpcLink
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Summary { get; set; }
        public string Method { get; set; }
        public Dictionary<string, object> Params { get; set; }
        public OpenRpcServer Server { get; set; }
    }
}