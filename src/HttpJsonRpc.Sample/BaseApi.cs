using System.Threading.Tasks;

namespace HttpJsonRpc.Sample
{
    public abstract class BaseApi
    {
        [JsonRpcMethod]
        public Task<string> GetTypeNameAsync()
        {
            return Task.FromResult(GetType().Name);
        }
    }
}