using System.Collections.Generic;

namespace HttpJsonRpc
{
    public class OpenRpcServer
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public Dictionary<string, OpenRpcServerVariable> Variables { get; set; }
    }
}