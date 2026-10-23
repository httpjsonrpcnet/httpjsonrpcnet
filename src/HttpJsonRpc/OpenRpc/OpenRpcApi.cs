using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HttpJsonRpc
{
    [JsonRpcClass("rpc")]
    public class OpenRpcApi
    {
        [JsonRpcMethod]
        public async Task<OpenRpcDocument> DiscoverAsync()
        {
            await Task.CompletedTask;

            var context = JsonRpcContext.Current;
            var options = JsonRpc.Options;
            var rpcClasses = JsonRpc.RpcClasses.Values.ToArray();
            var rpcMethods = rpcClasses
                .SelectMany(c => c.Methods.Values)
                .Where(m => options.OpenRpc.MethodFilters.All(f => f.Invoke(context, m)))
                .ToArray();

            return OpenRpcDocumentGenerator.Generate(options, context, rpcMethods);
        }

        [JsonRpcGetSerializerOptions]
        public JsonSerializerOptions GetSerializerOptions(JsonRpcContext context, JsonSerializerOptions serializerOptions)
        {
            return new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
    }
}
