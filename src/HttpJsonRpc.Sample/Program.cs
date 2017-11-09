using System;
using System.Threading.Tasks;

namespace HttpJsonRpc.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            JsonRpc.OnReceivedRequest(c =>
            {
                Console.WriteLine($"Received jsonRpcRequest {c.Request.Method}");
                return Task.CompletedTask;
            });

            JsonRpc.Start();
            Console.ReadLine();
            JsonRpc.Stop();
        }

        [JsonRpcMethod]
        public static Task<int> SumAsync(int num1 = 0, int num2 = 0, [JsonRpcParameter(Name = "x")] int multiplier = 1, [JsonRpcParameter(Ignore = true)] bool log = false, bool throwException = false)
        {
            if (throwException) JsonRpcUnauthorizedException.Throw();
            var value = (num1 + num2) * multiplier;
            return Task.FromResult(value);
        }
    }
}
