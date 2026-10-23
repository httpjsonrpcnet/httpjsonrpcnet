using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class OpenRpcDocument
    {
        [JsonPropertyName("openrpc")]
        public string OpenRpc { get; set; } = "1.4.1";
        public OpenRpcInfo Info { get; set; }
        public OpenRpcServer[] Servers { get; set; }
        public OpenRpcMethod[] Methods { get; set; }
        public OpenRpcComponents Components { get; set; }
        public OpenRpcExternalDocumentation ExternalDocs { get; set; }
    }
}
