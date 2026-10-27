#nullable enable
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public class NonNullableDerived : NullableBase
    {
        public new string Value { get; set; } = "";
    }
}
