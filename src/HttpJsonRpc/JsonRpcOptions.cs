using System.Collections.Generic;

namespace HttpJsonRpc
{
    public class JsonRpcOptions
    {
        public OpenRpcOptions OpenRpc { get; } = new OpenRpcOptions();
        public List<string> ExcludedAssemblyPrefixes { get; } = new List<string>
        {
            "System.",
            "Microsoft.",
            "netstandard",
            "CommonServiceLocator"
        };
        public JsonRpcErrorFactory ErrorFactory { get; set; } = new JsonRpcErrorFactory();
    }
}
