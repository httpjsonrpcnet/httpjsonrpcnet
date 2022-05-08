using System;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace HttpJsonRpc.Sample
{
    [JsonRpcClass("math")]
    public class MathApi : BaseApi
    {
        private IMathService MathService { get; }

        public MathApi(IMathService mathService)
        {
            MathService = mathService;
        }

        [JsonRpcMethod(Description = "Provides the sum of two numbers.")]
        public Task<int> SumAsync(int n1, int n2)
        {
            var value = MathService.Sum(n1, n2);
            return Task.FromResult(value);
        }

        [JsonRpcReceivedRequest]
        public async Task OnReceivedRequestAsync(JsonRpcContext context)
        {
            await Task.CompletedTask;

            Console.WriteLine("OnReceivedRequest in MathApi");
        }

        [JsonRpcDeserializeParameter]
        public async Task<object> DeserializeParameterAsync(JsonElement value, ParameterInfo parameter, JsonSerializerOptions serializerOptions, JsonRpcContext context)
        {
            //This method can be used for custom parameter deserialization
            await Task.CompletedTask;

            return value.Deserialize(parameter.ParameterType, serializerOptions);
        }
    }
}