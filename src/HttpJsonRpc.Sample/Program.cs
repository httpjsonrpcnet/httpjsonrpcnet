using System;
using System.Threading.Tasks;

namespace HttpJsonRpc.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            JsonRpc.RegisterProcedures(typeof(Program).Assembly);
            JsonRpc.OnReceivedRequest(c =>
            {
                Console.WriteLine($"Received request {c.Request.Method}");
                return Task.CompletedTask;
            });

            var address = "http://localhost:5000/";
            JsonRpc.Start(address);

            Console.WriteLine($"Listening for requests on {address}");
            Console.ReadLine();
            JsonRpc.Stop();
        }

        [JsonRpcMethod]
        public static Task<int> SumAsync(int num1 = 0, int num2 = 0, [JsonRpcParameter(Name = "x")] int multiplier = 1, [JsonRpcParameter(Ignore = true)] bool log = false)
        {
            var value = (num1 + num2) * multiplier;
            return Task.FromResult(value);
        }
    }
}
