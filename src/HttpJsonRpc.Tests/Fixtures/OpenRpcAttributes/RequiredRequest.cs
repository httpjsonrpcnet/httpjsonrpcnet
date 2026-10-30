using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcAttributes
{
    public class RequiredRequest
    {
        [JsonRequired, OpenRpcProperty(Required = false)]
        public string Value { get; set; }
    }
}
