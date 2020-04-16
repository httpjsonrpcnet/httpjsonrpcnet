using System;
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
        public Task<int> SumAsync(int n1 = 0, int n2 = 0)
        {
            var value = MathService.Sum(n1, n2);
            return Task.FromResult(value);
        }

        [JsonRpcReceivedRequest]
        public async Task OnReceivedRequestAsync(JsonRpcContext context)
        {
            await Task.CompletedTask;

            Console.WriteLine("OnReceivedRequest in JsonRpcClass");
        }
    }
}