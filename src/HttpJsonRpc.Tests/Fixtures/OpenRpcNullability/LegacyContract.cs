#nullable disable
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public class LegacyContract
    {
        public string Name { get; set; }
        [OpenRpcProperty(Nullable = true)]
        public string Alias { get; set; }
    }
}
