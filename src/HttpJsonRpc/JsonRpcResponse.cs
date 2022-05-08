using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; }

        [JsonPropertyName("id")]
        public object Id { get; set; }

        [JsonPropertyName("result")]
        public object Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRpcError Error { get; set; }
    }
}