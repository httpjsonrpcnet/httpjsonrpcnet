using System;
using System.Threading.Tasks;

namespace HttpJsonRpc.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            JsonRpc.RegisterProcedures(typeof(Program).Assembly);
            JsonRpc.OnReceivedRequest(r =>
            {
                Console.WriteLine($"Received request {r.Method}");
                return Task.CompletedTask;
            });

            var address = "http://localhost:5000/";
            JsonRpc.Start(address);

            Console.WriteLine($"Listening for requests on {address}");
            Console.ReadLine();
            JsonRpc.Stop();
        }

        [JsonRpcMethod]
        public static Task<int> SumAsync(int num1 = 0, int num2 = 0)
        {
            return Task.FromResult(num1 + num2);
        }
    }
}
