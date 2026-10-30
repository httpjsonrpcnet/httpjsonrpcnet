using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcAttributes
{
    [OpenRpcSchema(Name = "DocumentedRequest", Description = "Request values.")]
    public class DocumentationRequest
    {
        [JsonPropertyName("text"), OpenRpcProperty(Description = "Text to return.", Required = true, Nullable = false, MinLength = 3)]
        public string Value { get; set; }

        [JsonIgnore, OpenRpcProperty(Description = "Hidden value.", Required = true)]
        public string Hidden { get; set; }

        [JsonInclude, JsonPropertyName("label"), OpenRpcProperty(Description = "A label.", Required = true, MaxLength = 10)]
        public string Label;
    }
}
