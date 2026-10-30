using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNames
{
    [OpenRpcSchema(Name = "Status", Description = "Status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Status
    {
        Pending,
        Complete
    }
}
