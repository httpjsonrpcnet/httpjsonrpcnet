using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcTransformers
{
    public class SearchRequest { [JsonPropertyName("query_text")] public string Query { get; set; } }
}
