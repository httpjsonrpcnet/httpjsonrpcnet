using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpJsonRpc
{
    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }
        [JsonProperty("id")]
        public object Id { get; set; }
        [JsonProperty("result")]
        public object Result { get; set; }
        [JsonProperty("error")]
        public JsonRpcError JsonRpcError { get; set; }

        public static JsonRpcResponse FromResult(object id, object result)
        {
            var response = Create(id);
            response.Result = JToken.FromObject(result);

            return response;
        }

        public static JsonRpcResponse FromError(int code, object id = null, object data = null)
        {
            var response = Create(id);
            response.JsonRpcError = new JsonRpcError
            {
                Code = code,
                Message = JsonRpcErrorCodes.GetMessage(code),
                Data = data == null ? null : JToken.FromObject(data)
            };

            return response;
        }

        private static JsonRpcResponse Create(object id)
        {
            return new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = id
            };
        }
    }
}