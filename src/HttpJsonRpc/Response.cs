using Newtonsoft.Json;

namespace HttpJsonRpc
{
    public class Response
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }
        [JsonProperty("id")]
        public object Id { get; set; }
        [JsonProperty("result")]
        public object Result { get; set; }
        [JsonProperty("error")]
        public Error Error { get; set; }
    }
}