using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace HttpJsonRpc.Sample
{
    public class JsonConsoleLogger : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Directory.CreateDirectory(@"C:\logs");
            File.AppendAllText(@"C:\logs\log.txt", state.ToString());
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }
}