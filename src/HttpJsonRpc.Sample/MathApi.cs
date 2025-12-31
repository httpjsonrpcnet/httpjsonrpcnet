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
        public async Task<int> SumAsync([JsonRpcParameter(Description = "The first number")] int n1, [JsonRpcParameter(Description = "The second number")] int n2)
        {
            await Task.CompletedTask;
            var value = MathService.Sum(n1, n2);
            return value;
        }

        [JsonRpcMethod(Description = "Test method to validate parameter merging from query string and body.")]
        public async Task<string> TestMergeAsync(
            [JsonRpcParameter(Description = "First parameter")] string param1,
            [JsonRpcParameter(Description = "Second parameter")] string param2,
            [JsonRpcParameter(Description = "Third parameter")] string param3 = null)
        {
            await Task.CompletedTask;
            return $"param1={param1}, param2={param2}, param3={param3 ?? "null"}";
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