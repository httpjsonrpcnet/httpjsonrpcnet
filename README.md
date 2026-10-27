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

Enable `JsonRpc.Options.OpenRpc.IsEnabled` to expose `rpc.discover`. Method filters limit discovery output; they do **not** enforce authorization when a method is invoked. Generated documents declare OpenRPC 1.4.1 and are validated against the official meta-schema in tests.

Schema component names now include CLR namespaces. Explicit JSON property names, GUIDs, numeric/string enums, nullable values, recursive objects/collections, and typed dictionaries are covered by tests. Versioned methods include the `x-version` extension; consumers must send that version in HttpJsonRpc's request `version` field.

Custom JSON converters can change shapes in arbitrary ways. Supply a matching `IOpenRpcTypeConverter` for those types; the library cannot infer arbitrary converter behavior or request-dependent serialization. Existing wrapper converters, such as an Undefinable-style converter that unwraps its inner type, remain supported. This release does not attempt complete inference of every custom System.Text.Json contract. Test the actual serialized contracts used by your application.

### Explicit descriptions

Use HttpJsonRpc attributes to author API documentation:

| Target | Attribute | Discovery field |
| --- | --- | --- |
| RPC operation | `JsonRpcMethod.Description` | Method description |
| Direct parameter or flattened `[JsonRpcParams]` property | `JsonRpcParameter.Description` | Parameter content descriptor description |
| Nested object property | `JsonRpcParameter.Description` | Local property schema description |
| Shared type | `JsonRpcSchema.Description` | Type schema description |
| Method success result | Method-level `JsonRpcResult.Description` | Result content descriptor description |

```csharp
[JsonRpcSchema(Description = "Values used to create a contact.")]
public class ContactCreateValues
{
    [JsonRpcParameter(Description = "The contact's given name.")]
    public string FirstName { get; set; }
}

[JsonRpcClass("contact")]
public class ContactApi
{
    [JsonRpcMethod(Description = "Creates a contact.")]
    [JsonRpcResult(Description = "The newly created contact.")]
    public Task<Contact> CreateAsync(
        [JsonRpcParameter(Description = "Values for the new contact.")]
        ContactCreateValues values)
    {
        // Create and return the application's Contact model.
        throw new NotImplementedException();
    }
}
```

`JsonRpcSchema` supports classes, structs, interfaces, and enums. Its description
is read from the converted underlying type, so `Task<Contact>` uses the `Contact`
schema's description. Type attributes inherit along class inheritance; attributes
on interfaces are not automatically imported into implementing classes. Nested
properties follow the serializer's selected members, including renamed, inherited,
and shadowed properties. Ignored members are not documented.

Descriptions on nested object references use a local `allOf` wrapper and leave the
shared component description intact. Parameters and results keep their usage text
on their content descriptors rather than duplicating it on the referenced schema.
`JsonRpcResult` is applied to the method, not the return-value attribute target.
It does not introduce a result for `void`, `Task`, or `ValueTask` methods that have
no generated result schema.

Attribute descriptions are populated before the relevant transformers run, so
transformers can override or clear them. Unset schema descriptions are omitted;
explicit empty strings remain empty. Text is preserved verbatim, including Markdown.
The library does not automatically import `System.ComponentModel.DescriptionAttribute`
or XML comments. These attributes affect discovery only, not runtime validation,
JSON property names, or method execution.

### Requiredness and nullability

Presence and nullability are independent. `JsonRpcParameter` accepts `Required`
and `Nullable` booleans on method parameters and object properties. `JsonRpcResult`
accepts `Nullable` on methods. Omit either property to infer it; explicitly setting
`false` is an override, not the same as leaving it unset.

```csharp
#nullable enable

public class ContactDetails
{
    [JsonRpcParameter(Required = true)]
    public string Name { get; set; } = "";

    [JsonRpcParameter(Required = true)]
    public string? Nickname { get; set; }

    public int? Age { get; set; }
}
```

Here `name` must be present and cannot be null; `nickname` must be present but can
be null; `age` is optional and can be null. Explicit `Nullable = true` or
`Nullable = false` overrides inference for that value. On a collection, it changes
the collection's nullability, not the nullability of its elements.

- Requiredness defaults to the existing contract: non-optional method parameters
  are required, and object properties use serializer-required metadata, C# `required`,
  `[JsonRequired]`, or the existing `[Required]` presence check. Nullability does
  not imply requiredness. Value-validation rules such as non-empty strings are
  not inferred from DataAnnotations.
- `Nullable<T>` permits null. Other value types do not. Modern C# nullable reference
  metadata is read using reflection, including enclosing nullable contexts,
  generic substitutions, inherited members, array elements, and dictionary values.
  `Task<T>` and `ValueTask<T>` use the nullability of `T` for their result.
- When reference metadata is absent (for example, unannotated legacy code), discovery
  preserves the previous schema behavior: typed schemas do not add null; an
  unconstrained `object` schema still accepts any JSON value. Use an explicit
  override to describe a different contract. The reader consumes compiler nullable
  annotations, not flow-analysis attributes such as `[AllowNull]` or `[MaybeNull]`.
- Different nested annotations can produce separate components for the same CLR
  type, such as `List<string>` and `List<string?>`. Follow emitted references or use
  the transformer's `ComponentName`; `GetName(Type)` supplies the base name and
  cannot identify a particular annotated usage.
- Custom wire-type converters retain their outer usage nullability, but must
  describe their own nested wire shapes. Nullable metadata cannot infer a custom
  serializer's output representation.

Method-parameter presence uses the OpenRPC descriptor's `required` boolean.
Object-property presence uses the JSON Schema `required` array. Null is expressed
with JSON Schema type unions or `anyOf`; a non-null override can use `not` to exclude
null from a referenced or composed schema without modifying its shared definition.
No OpenAPI-style `nullable` keyword is generated.

Usage transformers run after inferred and explicit nullability. Method transformers
run after descriptor requiredness is resolved, and schema transformers can change
an object's generated `required` array. These settings affect discovery only:
they do not change parameter binding, defaults, serialization, or runtime validation.

### Discovery transformers

Configure the following synchronous callback lists on `JsonRpc.Options.OpenRpc`
before starting the server. Each list runs in registration order; later callbacks
see earlier changes. These callbacks customize documentation, not runtime binding
or validation.

| Callback list | Target and context |
| --- | --- |
| `SchemaTransformers` | A generated type schema, original `ClrType`, `ConvertedType`, and optional `ComponentName`. |
| `SchemaUsageTransformers` | A local schema for a property, parameter, result, array item, or dictionary value. Includes `Kind`, `ClrType`, `ContainingType`, `JsonName`, `Member`, `Parameter`, and `Method` where applicable. |
| `MethodTransformers` | `MethodDescription`, registered `Method`, parameter-to-descriptor mappings, result descriptor, and `RequestContext`. |
| `DocumentTransformers` | The complete `Document`, included `Methods`, and `RequestContext`. |

For example, a generic contact API can describe a string date, customize a method,
and publish its server address:

```csharp
var openRpc = JsonRpc.Options.OpenRpc;
openRpc.SchemaUsageTransformers.Add(context =>
{
    if (context.Kind == OpenRpcSchemaUsageKind.Property &&
        context.ContainingType == typeof(Contact) &&
        context.Member?.Name == nameof(Contact.BirthDate))
    {
        context.Schema.Format = "date";
    }
});
openRpc.MethodTransformers.Add(context =>
{
    if (context.Method.FullName == "contact.create")
        context.MethodDescription.Summary = "Create a contact";
});
openRpc.DocumentTransformers.Add(context =>
{
    context.Document.Servers = new[]
    {
        new OpenRpcServer { Name = "production", Url = "https://example.com/rpc" }
    };
});
```

Generation follows these rules:

- Method inclusion filters run first. Excluded methods are not passed to transformers.
- Existing type converters still determine the CLR type's representation. A schema
  transformer runs after that schema's properties/items have been generated, once
  per shared component per discovery call, or for each generated inline schema.
  `ClrType` is the first encountered type (possibly a wrapper such as `Task<T>`);
  use `ConvertedType` for conventions on the shared underlying type.
- Property and collection-element usages belong to their containing type, not to a
  particular endpoint. Their `Method` is null. Parameter and result usages carry
  the registered method. A DTO parameter carries its `PropertyInfo` in `Member`;
  a direct parameter carries `ParameterInfo` in `Parameter`. Results expose the
  return parameter; JSON names reflect serializer naming. Collection elements have
  no member or JSON name of their own.
- Usage transformers receive an independent schema and may replace `context.Schema`
  with another non-null schema. References are wrapped in `allOf` so local keywords
  remain effective under JSON Schema Draft 7. They do not modify the referenced
  component. A property inside a shared component still applies wherever that
  containing type is used; endpoint-specific differences belong on the endpoint's
  parameter/result schema.
- Recursive references can point to components still under construction. Schema
  callbacks should work on their supplied target; whole-document inspection belongs
  in a document transformer. Preserve references rather than creating cyclic CLR
  object graphs in the schema model.
- Method transformers run after that method's descriptors are generated. The
  parameter map and `Result` identify the initially generated descriptors; if you
  replace entries in `MethodDescription`, update the replacements directly.
- Document transformers run last. The document, component schemas, and configured
  info/contact/license copies belong to the current discovery call.
- Callbacks can execute concurrently across requests. Do not mutate configuration
  while serving requests, retain request context, or insert shared mutable metadata
  objects into multiple documents. Exceptions fail discovery through the normal
  RPC error-handling path rather than returning a partially customized document.

The schema model's supported keywords still apply. Constraint attributes and
automatic XML documentation ingestion are not implemented.

## Development and release validation

```powershell
dotnet build src/HttpJsonRpc.sln -c Release -p:GeneratePackageOnBuild=false
dotnet test src/HttpJsonRpc.Tests/HttpJsonRpc.Tests.csproj -c Release --no-build
dotnet list src/HttpJsonRpc/HttpJsonRpc.csproj package --vulnerable --include-transitive
dotnet pack src/HttpJsonRpc/HttpJsonRpc.csproj -c Release --no-build -o artifacts -p:GeneratePackageOnBuild=false
./eng/Verify-Package.ps1 (Get-ChildItem artifacts/*.nupkg).FullName
```

Tests target .NET Framework 4.8 (Windows) and .NET 10. They use loopback listeners with dynamically assigned ports and vendored schemas, without MDware or external services. CI runs the same builds/tests and checks package contents. Transitive NuGet vulnerability warnings fail the build. The sample targets .NET 10.
