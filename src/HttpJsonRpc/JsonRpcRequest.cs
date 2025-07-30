using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpcRequest
    {
        public string JsonRpc { get; set; }
        public string Method { get; set; }
        public object Id { get; set; }
        public JsonElement? Params { get; set; }
        public string Version { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }
}