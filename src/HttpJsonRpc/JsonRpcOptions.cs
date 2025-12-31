using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public enum ParameterPrecedence
    {
        QueryString,  // Default - query params override body params
        Body,         // Body params override query params
        Strict        // Throw error if same param in both places
    }

    public class JsonRpcOptions
    {
        public OpenRpcOptions OpenRpc { get; } = new OpenRpcOptions();
        public List<string> ExcludedAssemblyPrefixes { get; } = new List<string>
        {
            "System.",
            "Microsoft.",
            "netstandard",
            "CommonServiceLocator",
            "HttpJsonRpc",
        };
        public JsonRpcErrorFactory ErrorFactory { get; set; } = new JsonRpcErrorFactory();
        public ILoggerFactory LoggerFactory { get; set; }
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        public Action<KestrelServerOptions> ServerOptions { get; set; } = (o) => o.Listen(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000));
        public Action<CorsPolicyBuilder> CorsPolicy { get; set; }
        public bool IncludeStackTraceInErrors { get; set; } = true;
        public ParameterPrecedence ParameterMergePrecedence { get; set; } = ParameterPrecedence.QueryString;
    }
}
