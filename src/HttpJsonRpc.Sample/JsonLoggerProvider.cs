using Microsoft.Extensions.Logging;

namespace HttpJsonRpc.Sample
{
    public class JsonLoggerProvider : ILoggerProvider
    {
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new JsonConsoleLogger();
        }
    }
}