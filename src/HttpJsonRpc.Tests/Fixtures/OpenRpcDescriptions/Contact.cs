using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    [JsonRpcSchema(Description = "A contact record.")]
    public class Contact
    {
        [JsonRpcParameter(Description = "Where invoices are sent.")]
        [JsonPropertyName("billing_address")]
        public Address Billing { get; set; }
        [JsonRpcParameter(Description = "Where deliveries are sent.")]
        public Address Shipping { get; set; }
        [JsonIgnore, JsonRpcParameter(Description = "Must not appear.")]
        public string Hidden { get; set; }
        [Description("UI-only explanation.")]
        public string InternalLabel { get; set; }
    }
}
