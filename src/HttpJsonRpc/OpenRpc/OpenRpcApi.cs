using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HttpJsonRpc
{
    [JsonRpcClass("rpc")]
    public class OpenRpcApi
    {
        [JsonRpcMethod]
        [OpenRpcMethod(Description = "Returns the OpenRPC document for visible methods.")]
        public async Task<OpenRpcDocument> DiscoverAsync(
            [OpenRpcParameter(Description = "Method name, or a prefix followed by *. Omit or use null for all visible methods.", Nullable = true)] string name = null)
        {
            await Task.CompletedTask;

            var context = JsonRpcContext.Current;
            var options = JsonRpc.Options;
            var prefix = name != null && name.EndsWith("*", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - 1) : null;
            var rpcClasses = JsonRpc.RpcClasses.Values.ToArray();
            var rpcMethods = rpcClasses
                .SelectMany(c => c.Methods.Values)
                .Where(m => options.OpenRpc.MethodFilters.All(f => f.Invoke(context, m)))
                .Where(m => name == null || (prefix != null
                    ? m.FullName.StartsWith(prefix, StringComparison.Ordinal)
                    : string.Equals(m.FullName, name, StringComparison.Ordinal)))
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
