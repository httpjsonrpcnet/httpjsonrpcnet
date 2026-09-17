using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; }
        [JsonPropertyName("method")]
        public string Method { get; set; }
        [JsonPropertyName("id")]
        public object Id { get; set; }
        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }
}
