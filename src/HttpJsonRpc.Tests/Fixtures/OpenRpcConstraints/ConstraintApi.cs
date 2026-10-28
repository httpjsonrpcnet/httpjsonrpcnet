using System.Threading.Tasks;
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcConstraints
{
    [JsonRpcClass("constraints")]
    public class ConstraintApi
    {
        [JsonRpcMethod, JsonRpcResult(MinLength = 2, MaxLength = 5, Pattern = "^[A-Z]+$", Format = "custom-code")]
        public Task<string> Find([JsonRpcParameter(MinLength = 2, MaxLength = 5)] string code) => Task.FromResult(code);
        [JsonRpcMethod]
        public ConstraintRequest Echo(ConstraintRequest input) => input;
        [JsonRpcMethod]
        public string Flatten([JsonRpcParams] ConstraintRequest input) => input.Email;
        [JsonRpcMethod, JsonRpcResult(MaxItems = 2)]
        public LimitedNames Names() => new LimitedNames();
    }
}
