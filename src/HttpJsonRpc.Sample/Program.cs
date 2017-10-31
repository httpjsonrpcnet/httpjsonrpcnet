using System;
using System.Threading.Tasks;
using HttpJsonRpcNet.Sample;

namespace HttpJsonRpc.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            JsonRpc.AddProcedure<SumParams, int>(SumAsync);
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

        public static Task<int> SumAsync(SumParams parameter)
        {
            return Task.FromResult(parameter.Num1 + parameter.Num2);
        }
    }
}
