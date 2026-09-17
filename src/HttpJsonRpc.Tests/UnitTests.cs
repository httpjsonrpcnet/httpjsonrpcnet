using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public class UnitTests
    {
        private static JsonRpcContext Context(string method, string parameters = null) => new JsonRpcContext
        {
            HttpContext = new DefaultHttpContext(),
            SerializerOptions = new JsonRpcOptions().SerializerOptions,
            Request = new JsonRpcRequest { Params = parameters == null ? (JsonElement?)null : JsonDocument.Parse(parameters).RootElement.Clone() },
            Method = new JsonRpcClass(typeof(UnitApi)).Methods[method]
        };
        [Test] public void ExplicitNamesAreNotTruncated() => Assert.That(new JsonRpcClass(typeof(UnitApi)).Methods.ContainsKey("runasyncjob"), Is.True);
        [Test] public void AsyncSuffixIsRemovedOnlyAtEnd() => Assert.That(new JsonRpcClass(typeof(UnitApi)).Methods.ContainsKey("asyncmiddle"), Is.True);
        [Test] public void UnsupportedSignaturesFailAtRegistration() => Assert.Throws<InvalidOperationException>(() => new JsonRpcClass(typeof(InvalidApi)));
        [TestCase("sync", 42), TestCase("task", 42), TestCase("value", 42)]
        public async Task ReturnTypesExecute(string name, int expected)
        {
            var c = Context(name);
            Assert.That(await JsonRpc.InvokeAsync(c.Method.MethodInfo, null, new object[0]), Is.EqualTo(expected));
        }
        [TestCase("{\"x\":4}", 4), TestCase("[5]", 5), TestCase(null, 7)]
        public async Task BindNamedPositionalAndDefault(string json, int expected)
        {
            var c = Context("optional", json); await JsonRpc.BindParametersAsync(c);
            Assert.That(await JsonRpc.InvokeAsync(c.Method.MethodInfo, null, c.RequestParameters.ToArray()), Is.EqualTo(expected));
        }
        [Test] public void MissingRequiredParameterIsRejected() => Assert.ThrowsAsync<ArgumentException>(async () => await JsonRpc.BindParametersAsync(Context("required")));
        [Test] public void BadParameterTypeIsRejected() => Assert.ThrowsAsync<JsonException>(async () => await JsonRpc.BindParametersAsync(Context("required", "[\"bad\"]")));
        [TestCase(JsonIgnoreCondition.Never), TestCase(JsonIgnoreCondition.WhenWritingNull), TestCase(JsonIgnoreCondition.WhenWritingDefault)]
        public void EnvelopeRetainsNullResultAndId(JsonIgnoreCondition ignore)
        {
            var c = Context("sync"); c.SerializerOptions.DefaultIgnoreCondition = ignore;
            var envelope = JsonRpc.CreateEnvelope(c, null);
            Assert.That(envelope.ContainsKey("result"), Is.True);
            Assert.That(envelope.ContainsKey("id"), Is.True);
            Assert.That(envelope.ContainsKey("error"), Is.False);
            var error = JsonRpc.CreateEnvelope(c, JsonRpcError.Create(-32603));
            Assert.That(error.ContainsKey("result"), Is.False);
            Assert.That(error["error"]["code"].GetValue<int>(), Is.EqualTo(-32603));
        }
        [Test]
        public void SafeErrorDefaultsHideDetails()
        {
            var error = new JsonRpcErrorFactory().CreateError(new JsonRpcErrorFactory.CreateErrorArgs { ErrorCode = -32603, Exception = new Exception("secret"), Options = new JsonRpcOptions() });
            Assert.That(error.Message, Is.EqualTo("Internal error")); Assert.That(error.Data, Is.Null);
        }
        [Test]
        public void DiagnosticMessagesCanBeOptedInto()
        {
            var error = new JsonRpcErrorFactory().CreateError(new JsonRpcErrorFactory.CreateErrorArgs { ErrorCode = -32603, Exception = new Exception("diagnostic"), Options = new JsonRpcOptions { IncludeExceptionMessagesInErrors = true } });
            Assert.That(error.Message, Is.EqualTo("diagnostic"));
        }
        [TestCase("{}"), TestCase("{\"jsonrpc\":\"9.9\",\"method\":\"x\"}"), TestCase("{\"jsonrpc\":\"2.0\",\"method\":1}"), TestCase("{\"jsonrpc\":\"2.0\",\"method\":\"x\",\"id\":true}")]
        public void StrictValidationRejectsMalformedObjects(string json) => Assert.Throws<ArgumentException>(() => JsonRpc.ValidateRequest(JsonNode.Parse(json).AsObject(), true));
        [Test]
        public void NamespacedTypesDoNotShareSchemas()
        {
            var g = new OpenRpcSchemaGenerator(new JsonRpcOptions()); var a = g.GetSchema(typeof(First.Dto)); var b = g.GetSchema(typeof(Second.Dto));
            Assert.That(a.Ref, Is.Not.EqualTo(b.Ref)); Assert.That(g.Schemas[g.GetName(typeof(Second.Dto))].Properties.ContainsKey("second"), Is.True);
        }
        [Test]
        public void SchemaHonorsExplicitNamesAndIgnoredProperties()
        {
            var g = new OpenRpcSchemaGenerator(new JsonRpcOptions()); g.GetSchema(typeof(Contract));
            var s = g.Schemas[g.GetName(typeof(Contract))];
            Assert.That(s.Properties.ContainsKey("UPPER_NAME"), Is.True); Assert.That(s.Properties.ContainsKey("hidden"), Is.False);
            Assert.That(s.Properties.ContainsKey("Item"), Is.False);
            Assert.That(s.Required, Does.Contain("UPPER_NAME"));
        }
        [Test]
        public void ScalarSchemasMatchJson()
        {
            var o = new JsonRpcOptions(); var g = new OpenRpcSchemaGenerator(o);
            Assert.That(g.GetSchema(typeof(Guid)).Type, Is.EqualTo("string"));
            Assert.That(g.GetSchema(typeof(byte[])).Type, Is.EqualTo("string"));
            Assert.That(g.GetSchema(typeof(DayOfWeek)).Type, Is.EqualTo("integer"));
            o.SerializerOptions = new JsonSerializerOptions(o.SerializerOptions);
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            Assert.That(new OpenRpcSchemaGenerator(o).GetSchema(typeof(DayOfWeek)).Type, Is.EqualTo("string"));
        }
        [Test]
        public void RecursiveSchemasTerminate()
        {
            var g = new OpenRpcSchemaGenerator(new JsonRpcOptions()); g.GetSchema(typeof(Recursive)); g.GetSchema(typeof(RecursiveList));
            Assert.That(g.Schemas.Count, Is.EqualTo(2));
        }
        [Test]
        public void CustomWrapperConverterRemainsSupported()
        {
            var o = new JsonRpcOptions(); o.OpenRpc.TypeConverters.Insert(0, new WrapperConverter());
            var g = new OpenRpcSchemaGenerator(o);
            Assert.That(g.GetSchema(typeof(Wrapper<Guid>)).Type, Is.EqualTo("string"));
            Assert.That(g.GetSchema(typeof(Wrapper<Contract>)).Ref, Is.EqualTo(g.GetSchema(typeof(Contract)).Ref));
        }
        [Test]
        public void NullableValueSchemasAllowNull()
        {
            var s = new OpenRpcSchemaGenerator(new JsonRpcOptions()).GetSchema(typeof(int?));
            Assert.That((string[])s.Type, Is.EquivalentTo(new[] { "integer", "null" }));
        }
        [Test]
        public void TypedDictionaryDescribesValues()
        {
            var g = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            g.GetSchema(typeof(Dictionary<string, Guid>));
            Assert.That(g.Schemas[g.GetName(typeof(Dictionary<string, Guid>))].AdditionalProperties.Type, Is.EqualTo("string"));
        }
        public class Contract { [JsonPropertyName("UPPER_NAME"), JsonRequired] public string Name { get; set; } [JsonIgnore] public string Hidden { get; set; } public string this[int index] => "x"; }
        public class Recursive { public Recursive Next { get; set; } }
        public class RecursiveList : List<RecursiveList> { }
        public class Wrapper<T> { public T Value { get; set; } }
        public class WrapperConverter : IOpenRpcTypeConverter
        {
            public bool CanConvert(OpenRpcSchemaGenerator g, Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Wrapper<>);
            public string GetName(OpenRpcSchemaGenerator g, Type t) => g.GetName(t.GetGenericArguments()[0]);
            public OpenRpcTypeInfo Convert(OpenRpcSchemaGenerator g, OpenRpcTypeInfo i) => i.With(x => x.Type = i.Type.GetGenericArguments()[0]);
        }
        [JsonRpcClass("unit")]
        public static class UnitApi
        {
            [JsonRpcMethod("runAsyncJob")] public static void Named() { }
            [JsonRpcMethod] public static void AsyncMiddle() { }
            [JsonRpcMethod] public static int Sync() => 42;
            [JsonRpcMethod] public static Task<int> Task() => System.Threading.Tasks.Task.FromResult(42);
            [JsonRpcMethod] public static ValueTask<int> Value() => new ValueTask<int>(42);
            [JsonRpcMethod] public static int Optional(int x = 7) => x;
            [JsonRpcMethod] public static int Required(int x) => x;
        }
        [JsonRpcClass("invalid")] public static class InvalidApi { [JsonRpcMethod] public static void Ref(ref int value) { } }
    }
}
namespace First { public class Dto { public string First { get; set; } } }
namespace Second { public class Dto { public int Second { get; set; } } }
