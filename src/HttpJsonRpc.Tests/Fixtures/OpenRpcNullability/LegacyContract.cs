#nullable disable
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public class LegacyContract
    {
        public string Name { get; set; }
        [JsonRpcParameter(Nullable = true)]
        public string Alias { get; set; }
    }
}
