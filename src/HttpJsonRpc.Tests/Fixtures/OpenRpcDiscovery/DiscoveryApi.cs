using HttpJsonRpc.Tests.Fixtures.OpenRpcTransformers;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDiscovery
{
    [JsonRpcClass("discovery", Version = "v1")]
    public class DiscoveryApi
    {
        [JsonRpcMethod] public Contact Contact(Contact value) => value;
        [JsonRpcMethod] public Node Tree(Node value) => value;
    }
}
