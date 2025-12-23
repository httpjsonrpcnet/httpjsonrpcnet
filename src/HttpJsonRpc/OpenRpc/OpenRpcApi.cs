using System.Collections.Generic;
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

            var schemaGenerator = new OpenRpcSchemaGenerator(options);

            var methods = new List<OpenRpcMethod>();

            foreach (var m in rpcMethods)
            {
                var method = new OpenRpcMethod
                {
                    Name = $"{m.ParentClass.Name.ToLowerFirstChar()}.{m.Name.ToLowerFirstChar()}",
                    Description = m.Description,
                    Params = m.Parameters.Select(p => new OpenRpcContentDescriptor
                    {
                        Name = p.Name.ToLowerFirstChar(),
                        Description = p.Description,
                        Required = !p.Optional,
                        Schema = schemaGenerator.GetSchema(p.ClrType)
                    }).ToArray()
                };

                var resultSchema = schemaGenerator.GetSchema(m.MethodInfo.ReturnType);
                if (resultSchema != null)
                {
                    method.Result = new OpenRpcContentDescriptor
                    {
                        Name = "result",
                        Schema = resultSchema
                    };
                }

                methods.Add(method);
            }

            return new OpenRpcDocument
            {
                Info = options.OpenRpc.Info,
                Methods = methods.ToArray(),
                Components = new OpenRpcComponents
                {
                    Schemas = schemaGenerator.Schemas
                }
            };
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
