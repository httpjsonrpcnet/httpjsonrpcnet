using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    [OpenRpcSchema(Description = "A contact record.")]
    public class Contact
    {
        [OpenRpcProperty(Description = "Where invoices are sent.")]
        [JsonPropertyName("billing_address")]
        public Address Billing { get; set; }
        [OpenRpcProperty(Description = "Where deliveries are sent.")]
        public Address Shipping { get; set; }
        [JsonIgnore, OpenRpcProperty(Description = "Must not appear.")]
        public string Hidden { get; set; }
        [Description("UI-only explanation.")]
        public string InternalLabel { get; set; }
    }
}
