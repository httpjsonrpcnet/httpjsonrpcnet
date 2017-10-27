using System;
using System.Threading.Tasks;

namespace HttpJsonRpcNet.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            HttpJsonRpc.AddProcedure<SumParams, int>("sum", p =>
            {
                Console.WriteLine($"Received request to Sum {p.Num1} and {p.Num2}");
                return Task.FromResult(p.Num1 + p.Num2);
            });

            var address = "http://localhost:5000/";
            HttpJsonRpc.Start(address);

            Console.WriteLine($"Listening for requests on {address}");
            Console.ReadLine();
            HttpJsonRpc.Stop();
        }
    }
}
