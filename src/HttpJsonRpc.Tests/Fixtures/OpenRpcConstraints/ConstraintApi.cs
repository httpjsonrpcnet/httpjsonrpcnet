using System.Threading.Tasks;
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcConstraints
{
    [JsonRpcClass("constraints")]
    public class ConstraintApi
    {
        [JsonRpcMethod, OpenRpcResult(MinLength = 2, MaxLength = 5, Pattern = "^[A-Z]+$", Format = "custom-code")]
        public Task<string> Find([OpenRpcParameter(MinLength = 2, MaxLength = 5)] string code) => Task.FromResult(code);
        [JsonRpcMethod]
        public ConstraintRequest Echo(ConstraintRequest input) => input;
        [JsonRpcMethod]
        public string Flatten([JsonRpcParams] ConstraintRequest input) => input.Email;
        [JsonRpcMethod, OpenRpcResult(MaxItems = 2)]
        public LimitedNames Names() => new LimitedNames();
    }
}
