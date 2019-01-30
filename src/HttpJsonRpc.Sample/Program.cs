using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.CommonServiceLocator;
using CommonServiceLocator;
using Microsoft.Extensions.Logging;
using Serilog;

namespace HttpJsonRpc.Sample
{
    [JsonRpcClass("program")]
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
            JsonRpc.OnError(e =>
            {
                Debug.WriteLine(e.ToString());
            });

            //Setup optional dependency injection
            var builder = new ContainerBuilder();
            builder.RegisterType<MathApi>();
            builder.RegisterType<MathService>().As<IMathService>();
            var container = builder.Build();
            var csl = new AutofacServiceLocator(container);
            ServiceLocator.SetLocatorProvider(() => csl);

            try
            {
                JsonRpc.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            Console.ReadLine();
            JsonRpc.Stop();
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

        [JsonRpcMethod]
        public static Task ThrowErrorAsync(string message)
        {            
            throw new Exception(message);
        }
    }
}
