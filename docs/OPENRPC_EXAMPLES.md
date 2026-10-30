# OpenRPC request and result examples

Examples are named pairings of parameter values and successful results. They help
readers understand how to call a method and what to expect back. Configure these
transformers on `JsonRpc.Options.OpenRpc` before starting the server.

The following snippets use the existing API and require `using HttpJsonRpc;` and
`using System.Text.Json;`. They assume registered methods named `contact.create`
with one object parameter named `input`, and `contact.find` with an `id` parameter.
Adapt the names and values to the methods and schemas in your application.

## Create a contact

This example omits an optional nickname from the input and shows an explicit null
nickname in the returned contact.

```csharp
JsonRpc.Options.OpenRpc.MethodTransformers.Add(context =>
{
    if (context.Method.FullName != "contact.create")
        return;

    context.MethodDescription.Examples = new object[]
    {
        new OpenRpcExamplePairing
        {
            Name = "createWithoutNickname",
            Params = new[]
            {
                new OpenRpcExample
                {
                    Name = "input",
                    Value = JsonSerializer.SerializeToElement(new
                    {
                        name = "Alex",
                        email = "alex@example.com"
                    })
                }
            },
            Result = new OpenRpcExample
            {
                Name = "result",
                Value = JsonSerializer.SerializeToElement(new
                {
                    id = 42,
                    name = "Alex",
                    email = "alex@example.com",
                    nickname = (string)null
                })
            }
        }
    };
});
```

The corresponding fragment of the method's discovery entry is:

```json
{
  "examples": [
    {
      "name": "createWithoutNickname",
      "params": [
        {
          "name": "input",
          "value": {
            "name": "Alex",
            "email": "alex@example.com"
          }
        }
      ],
      "result": {
        "name": "result",
        "value": {
          "id": 42,
          "name": "Alex",
          "email": "alex@example.com",
          "nickname": null
        }
      }
    }
  ]
}
```

## Find a contact, including no match

A second transformer can describe both successful lookup outcomes. This assumes
that the method's result schema permits null when a contact does not exist.

```csharp
JsonRpc.Options.OpenRpc.MethodTransformers.Add(context =>
{
    if (context.Method.FullName != "contact.find")
        return;

    context.MethodDescription.Examples = new object[]
    {
        new OpenRpcExamplePairing
        {
            Name = "contactFound",
            Params = new[] { new OpenRpcExample { Name = "id", Value = 42 } },
            Result = new OpenRpcExample
            {
                Name = "result",
                Value = JsonSerializer.SerializeToElement(new
                {
                    id = 42,
                    name = "Alex",
                    email = "alex@example.com",
                    nickname = (string)null
                })
            }
        },
        new OpenRpcExamplePairing
        {
            Name = "contactNotFound",
            Params = new[] { new OpenRpcExample { Name = "id", Value = 999 } },
            Result = new OpenRpcExample
            {
                Name = "result",
                Value = JsonSerializer.SerializeToElement<object>(null)
            }
        }
    };
});
```

The no-match pairing produces:

```json
{
  "name": "contactNotFound",
  "params": [{ "name": "id", "value": 999 }],
  "result": { "name": "result", "value": null }
}
```

## Authoring details

- Parameter example names must match the method's parameter descriptors. Values
  are parameter/result payloads, not complete JSON-RPC request/response envelopes.
- Add more pairings for meaningful variations, such as a contact with a nickname
  or a nonempty collection result. Use distinct, descriptive pairing names.
- `Summary` and `Description` can explain a pairing or an individual value.
- The discovery serializer omits null CLR properties. Use a `JsonElement` holding
  JSON null, as above, to retain `"value": null`. Pre-serializing structured example
  values also preserves intentional null properties within them.
- These snippets explicitly choose the wire names and default serialization of
  their anonymous objects. When using application DTOs, serialize with the options
  and converters used for those payloads so the examples match the real contract.
- Example values are authored data. The library does not execute the method or
  validate those values against its generated schemas during discovery.
- These examples use existing transformers; no example-provider attribute is
  required. Each callback creates fresh objects for its discovery invocation.

## Public schema names

Use `OpenRpcSchema.Name` to give a reusable schema a stable public component name:

```csharp
[OpenRpcSchema(Name = "Contact", Description = "A contact record.")]
public class ContactDto
{
    public string DisplayName { get; set; }
}
```

The component key is `Contact`, and references use `#/components/schemas/Contact`.
`Description` remains the schema description. Existing constraint properties remain
available and are unchanged. Naming does not change JSON payload property names.

Omitting `Name` preserves the existing converter/generated naming. An explicit name
is used before a type converter's naming hook; wrapper converters that delegate to
the wrapped type's name also preserve it. Names must be nonempty and contain only
ASCII letters, digits, underscores, periods or hyphens. Conflicting component names
for different types fail explicitly. Give different closed generic types distinct
names through a converter if needed; a fixed attribute name is shared by all closed
forms. Existing nullability specialization suffixes still apply where necessary.
Enums with an explicit `Name` are emitted as reusable components, including when
used through nullable or task wrappers. Other inline scalar schemas do not acquire
a component merely by setting `Name`.
