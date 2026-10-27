#nullable enable
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public struct Pair<TFirst, TSecond>
    {
        public TFirst First { get; set; }
        public TSecond Second { get; set; }
    }
}
