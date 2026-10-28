using System.Collections.Generic;
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcConstraints
{
    [JsonRpcSchema(MinItems = 1, MaxItems = 3, UniqueItems = true)]
    public class LimitedNames : List<string> { }
}
