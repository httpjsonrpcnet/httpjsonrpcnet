using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpJsonRpc
{
    public class JsonRpcRequest
    {
        public string JsonRpc { get; set; }
        public string Method { get; set; }
        public object Id { get; set; }
        public JToken Params { get; set; }

        [JsonExtensionData]
        public JObject ExtensionData { get; set; }
    }
}