#nullable enable
using System.Threading.Tasks;
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    [JsonRpcClass("nullable")]
    public class NullableApi
    {
        [JsonRpcMethod]
        public Task<string?> Find(string name, string? alias, int? age, string fallback = "") => Task.FromResult<string?>(null);
        [JsonRpcMethod]
        public ValueTask<Box<string?>> Read() => new ValueTask<Box<string?>>(new Box<string?>());
        [JsonRpcMethod, JsonRpcResult(Nullable = false)]
        public Task<string?> StrictResult() => Task.FromResult<string?>("");
        [JsonRpcMethod, JsonRpcResult(Nullable = true)]
        public string NullableResult() => "";
        [JsonRpcMethod]
        public Contract Echo(Contract contract) => contract;
        [JsonRpcMethod]
        public string Override([JsonRpcParameter(Required = false, Nullable = true)] string name) => name;
        [JsonRpcMethod]
        public string RequiredDefault([JsonRpcParameter(Required = true)] string name = "") => name;
        [JsonRpcMethod]
        public string Flatten([JsonRpcParams] Box<string?> input) => "";
    }
}
