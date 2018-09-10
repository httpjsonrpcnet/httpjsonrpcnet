using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;

namespace HttpJsonRpc.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            //Configure JsonRpc to use Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            JsonRpc.LoggerFactory = new LoggerFactory().AddSerilog();

            //Custom error handling
            //JsonRpc.OnError(e =>
            //{
            //    Debug.WriteLine(e.ToString());
            //    return Task.CompletedTask;
            //});

            JsonRpc.Start();
            Console.ReadLine();
            JsonRpc.Stop();
        }

        [JsonRpcMethod(Description = "Provides the sum of two numbers.")]
        public static Task<int> SumAsync(int num1 = 0, int num2 = 0, [JsonRpcParameter(Name = "x")] int multiplier = 1, [JsonRpcParameter(Ignore = true)] bool log = false, bool throwException = false)
        {
            if (throwException) JsonRpcUnauthorizedException.Throw();
            var value = (num1 + num2) * multiplier;
            return Task.FromResult(value);
        }

        [JsonRpcMethod]
        public static Task WriteLineAsync(string message)
        {
            Console.WriteLine(message);

            return Task.CompletedTask;
        }

        [JsonRpcMethod]
        public static async Task UploadAsync(string fileName)
        {
            using (var f = File.Create(fileName))
            {
                await JsonRpcContext.Current.HttpContext.Request.InputStream.CopyToAsync(f);
            }
        }

        [JsonRpcMethod]
        public static Task<JsonRpcStreamResult> DownloadAsync(string fileName)
        {
            var file = File.OpenRead(fileName);

            //Assume we are downloading a jpeg
            var result = new JsonRpcStreamResult(file, "image/jpeg");

            return Task.FromResult(result);
        }
    }
}
