#nullable enable
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public class NullableBase
    {
        public virtual string? Value { get; set; }
        public string? Inherited { get; set; }
    }
}
