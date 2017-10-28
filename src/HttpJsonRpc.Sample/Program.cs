using System;
using System.Threading.Tasks;
using HttpJsonRpcNet.Sample;

namespace HttpJsonRpc.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            JsonRpc.AddProcedure("sum", (SumParams p) => Task.FromResult(p.Num1 + p.Num2));

            var address = "http://localhost:5000/";
            JsonRpc.Start(address);

            Console.WriteLine($"Listening for requests on {address}");
            Console.ReadLine();
            JsonRpc.Stop();
        }
    }
}
