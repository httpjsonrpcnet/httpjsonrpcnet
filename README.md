# HttpJsonRpc
HttpJsonRpc is a cross-platform, .NET Standard library that implements the [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification), as well as streaming (up and down), on top of HTTP.

It makes it simple to create high performance, robust, light-weight API's that can be hosted in any project type. It uses ASP.NET Core Kestrel internally without requiring an ASP.NET application project.

HttpJsonRpc also has extensibility points for logging and dependency injection, so you can use your favorite frameworks.

## Getting Started
1. Create a class that will hold your `JsonRpc` methods with the `JsonRpcClassAttribute`.
```csharp
[JsonRpcClass("math")]
public static class MathApi
{
    [JsonRpcMethod]
    public static Task<int> SumAsync(int n1, int n2)
    {
        var result = n1 + n2;

        return Task.FromResult(result);
    }
}
```

2. Call the `JsonRpc.Start` method to start listening for `JSON-RPC` requests over `HTTP`.
```csharp
class Program
{
    static void Main(string[] args)
    {
        JsonRpc.Start();

        Console.ReadLine();
    }
}
```

3. The ideal way to call `JSON-RPC` methods is using an `HTTP POST` with the `JSON-RPC Request` in the `HTTP Body` but `HttpJsonRpc` also supports `URL Parameters` for simple method calls. Test the method by pasting the `URL` in a browser.
```
http://localhost:5000/?method=math.sum&n1=2&n2=2
```
You should get the result in `JSON`.
```json
{
    "jsonrpc": "2.0",
    "result": 4
}
```

When calling your method in code or `Postman` use an `HTTP POST` with the `JSON-RPC Request` in the `HTTP Body`.
```json
{
	"jsonrpc": "2.0",
	"method": "math.sum",
	"params": {
		"n1": 2,
		"n2": 2
	},
	"id": "1"
}
```

4. You can add your favorite logging provider by setting the `JsonRpc.LoggerFactory` property. This sample uses Serilog.
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

JsonRpc.LoggerFactory = new LoggerFactory().AddSerilog();
JsonRpc.Start();
```

5. You can find more code samples [here](https://github.com/httpjsonrpcnet/httpjsonrpcnet/tree/master/src/HttpJsonRpc.Sample).

## Hosting and middleware

Configure the library before calling `JsonRpc.Start()`. Registration and option changes are startup operations; do not mutate callbacks, converters, or the method registry while requests are running.

```csharp
JsonRpc.Options.ConfigureApplication = app =>
{
    // Register ASP.NET Core middleware before CORS and the RPC handler.
};
JsonRpc.Start();
// When shutting down:
await JsonRpc.StopAsync();
```

The library uses ASP.NET Core Kestrel internally and targets .NET Standard 2.0. It can be hosted by .NET Framework 4.8 and modern .NET applications. `StopAsync` stops and disposes the host. Repeated stop calls are safe; starting an already-running host throws. Registrations persist across restarts. `ListeningAddresses` reports bound addresses, including dynamically allocated ports.

When using forwarded headers, add `Microsoft.AspNetCore.HttpOverrides` in the consuming application and register `UseForwardedHeaders` through this callback. Configure trusted proxies/networks there; HttpJsonRpc does not trust forwarded headers itself. Authentication and authorization remain the host application's responsibility. CORS is not authentication.

## Request handling and lifecycle

To finish a request without invoking an RPC method, set `JsonRpcContext.Current.Handled = true` in `OnReceivedHttpRequest`, or `context.Handled = true` in `OnReceivedRequest`, and prepare the HTTP response. Started responses, explicit content lengths (including zero), and HTTP errors also stop dispatch. Alternatively throw `JsonRpcUnauthorizedException` to return an RPC unauthorized error.

- `OnCompletedRequest` remains a **success-only** callback. For batch calls it runs after successful result construction, before the aggregate HTTP response is written.
- `OnRequestFinished` runs on success, failure, rejection, or cancellation. For a dispatched batch it runs once per processed item; if parsing or HTTP rejection prevents dispatch, it runs once for the HTTP request. Use it for cleanup and diagnostics. `context.Exception` identifies a processing failure.
- `JsonRpcContext.Current` is scoped to the request. Do not retain its `HttpContext` after completion.
- Instances created with `Activator` are disposed after finished callbacks. Instances returned by CommonServiceLocator remain container-owned.
- Returned streams are library-owned and disposed after writing. Bytes are copied from the current position. The response body itself remains server-owned.
- A `CancellationToken` RPC parameter receives `HttpContext.RequestAborted` automatically and is not exposed as a wire parameter. Methods should observe cancellation; the library cannot forcibly stop application code.

## Protocol behavior

`JsonRpc.Options.StrictProtocol` defaults to `false` for existing clients that use GET requests, omitted protocol versions, case-insensitive method lookup, and id-less calls expecting replies.

Set it to `true` to validate JSON-RPC 2.0 request fields, use case-sensitive method lookup, validate extra arguments, and suppress replies to notifications (requests without `id`). A notification-only HTTP request returns status 204. Both modes use corrected response envelopes and error codes: parse error `-32700`, invalid request `-32600`, missing method `-32601`, invalid parameters `-32602`, internal error `-32603`. Application authorization uses the existing code `1`.

Batches execute sequentially and preserve individual request IDs. `MaxBatchSize` defaults to 100. Query-string merging is not supported for batches. Streaming results cannot be represented in a JSON batch and produce a per-item error; use individual calls for downloads. Methods in a batch are independent, not a transaction. Cancellation prevents further items from starting.

GET/query requests, raw binary uploads and stream downloads are HTTP extensions, not portable JSON-RPC behavior. Standard multipart requests may contain exactly one JSON field called `request`; framework form limits apply. Raw binary upload methods can read the request body directly using a query-based method selector and a non-JSON/non-form content type.

RPC methods support synchronous values/void, `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>`. Generic methods, ref/out/pointer signatures, and async-void methods are rejected during registration. Explicit method names are preserved; only an inferred trailing `Async` suffix is removed. For `[JsonRpcParams]` objects, use System.Text.Json property naming attributes to control the serialized names.

## Error disclosure

Both `IncludeStackTraceInErrors` and `IncludeExceptionMessagesInErrors` default to `false`. Error callbacks and logging still receive the original exception. Opt in explicitly for trusted development environments, or provide a `JsonRpcErrorFactory` that exposes only approved application messages. Changing this setting can affect applications that display server exception messages to users; see [migration notes](CHANGELOG.md).

## OpenRPC discovery

Enable `JsonRpc.Options.OpenRpc.IsEnabled` to expose `rpc.discover`. Method filters limit discovery output; they do **not** enforce authorization when a method is invoked. Generated documents declare OpenRPC 1.3.0 and are validated against the official meta-schema in tests.

Schema component names now include CLR namespaces. Explicit JSON property names, GUIDs, numeric/string enums, nullable values, recursive objects/collections, and typed dictionaries are covered by tests. Versioned methods include the `x-version` extension; consumers must send that version in HttpJsonRpc's request `version` field.

Custom JSON converters can change shapes in arbitrary ways. Supply a matching `IOpenRpcTypeConverter` for those types; the library cannot infer arbitrary converter behavior or request-dependent serialization. Existing wrapper converters, such as an Undefinable-style converter that unwraps its inner type, remain supported. This release does not attempt complete inference of every custom System.Text.Json contract. Test the actual serialized contracts used by your application.

## Development and release validation

```powershell
dotnet build src/HttpJsonRpc.sln -c Release -p:GeneratePackageOnBuild=false
dotnet test src/HttpJsonRpc.Tests/HttpJsonRpc.Tests.csproj -c Release --no-build
dotnet list src/HttpJsonRpc/HttpJsonRpc.csproj package --vulnerable --include-transitive
dotnet pack src/HttpJsonRpc/HttpJsonRpc.csproj -c Release --no-build -o artifacts -p:GeneratePackageOnBuild=false
./eng/Verify-Package.ps1 (Get-ChildItem artifacts/*.nupkg).FullName
```

Tests target .NET Framework 4.8 (Windows) and .NET 10. They use loopback listeners with dynamically assigned ports and vendored schemas, without MDware or external services. CI runs the same builds/tests and checks package contents. Transitive NuGet vulnerability warnings fail the build. The sample targets .NET 10.
