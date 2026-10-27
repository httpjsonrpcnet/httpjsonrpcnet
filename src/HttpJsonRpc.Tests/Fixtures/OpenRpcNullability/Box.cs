#nullable enable
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public class Box<T>
    {
        public T Value { get; set; } = default!;
        public Box<T>? Next { get; set; }
    }
}
