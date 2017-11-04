using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpJsonRpc
{
    public class Response
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }
        [JsonProperty("id")]
        public object Id { get; set; }
        [JsonProperty("result")]
        public object Result { get; set; }
        [JsonProperty("error")]
        public Error Error { get; set; }

        public static Response FromResult(object id, object result)
        {
            var response = Create(id);
            response.Result = JToken.FromObject(result);

            return response;
        }

        public static Response FromError(int code, object id = null, object data = null)
        {
            var response = Create(id);
            response.Error = new Error
            {
                Code = code,
                Message = ErrorCodes.GetMessage(code),
                Data = data == null ? null : JToken.FromObject(data)
            };

            return response;
        }

        private static Response Create(object id)
        {
            return new Response
            {
                JsonRpc = "2.0",
                Id = id
            };
        }
    }
}