using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    public class ContactRequest
    {
        [OpenRpcProperty(Description = "The contact to create.")]
        [JsonPropertyName("contact_values")]
        public Contact Values { get; set; }
    }
}
