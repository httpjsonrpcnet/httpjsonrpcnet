using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HttpJsonRpc.Tests.Fixtures.OpenRpcAttributes;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public class OpenRpcAttributeTests
    {
        private static OpenRpcDocument Generate() => OpenRpcDocumentGenerator.Generate(
            new JsonRpcOptions(), null, new JsonRpcClass(typeof(DocumentationApi)).Methods.Values.ToArray());

        private static JsonRpcContext Context(string method, string parameters)
        {
            using (var json = JsonDocument.Parse(parameters))
                return new JsonRpcContext
                {
                    HttpContext = new DefaultHttpContext(),
                    SerializerOptions = new JsonRpcOptions().SerializerOptions,
                    Request = new JsonRpcRequest { Params = json.RootElement.Clone() },
                    Method = new JsonRpcClass(typeof(DocumentationApi)).Methods[method]
                };
        }

        private static async Task<object> Invoke(JsonRpcContext context)
        {
            await JsonRpc.BindParametersAsync(context);
            return await JsonRpc.InvokeAsync(context.Method.MethodInfo, null, context.RequestParameters.ToArray());
        }

        [Test]
        public void DiscoveryUsesDocumentationAttributesAndFunctionalNames()
        {
            var document = Generate();
            var echo = document.Methods.Single(m => m.Name == "documented.echo");
            Assert.That(echo.Description, Is.EqualTo("Returns the supplied value."));
            var parameter = echo.Params.Cast<OpenRpcContentDescriptor>().Single();
            Assert.That(parameter.Name, Is.EqualTo("text"));
            Assert.That(parameter.Description, Is.EqualTo("Text to return."));
            Assert.That(parameter.Required, Is.True);
            Assert.That(parameter.Schema.Type, Is.EqualTo("string"));
            Assert.That(parameter.Schema.MinLength, Is.EqualTo(3));
            var result = (OpenRpcContentDescriptor)echo.Result;
            Assert.That(result.Name, Is.EqualTo("result"));
            Assert.That(result.Description, Is.EqualTo("The supplied value."));
            Assert.That(result.Schema.MinLength, Is.EqualTo(3));
            Assert.That(document.Methods.Any(m => m.Name == "documented.unregistered"), Is.False);
            Assert.That(new JsonRpcClass(typeof(DocumentationApi)).Methods.ContainsKey("unregistered"), Is.False);
        }

        [Test]
        public void PropertyMetadataDescribesFlattenedParametersAndNestedSchemas()
        {
            var document = Generate();
            var flat = document.Methods.Single(m => m.Name == "documented.flatten").Params.Cast<OpenRpcContentDescriptor>().Single();
            Assert.That(flat.Name, Is.EqualTo("text"));
            Assert.That(flat.Description, Is.EqualTo("Text to return."));
            Assert.That(flat.Required, Is.True);
            Assert.That(flat.Schema.MinLength, Is.EqualTo(3));
            var schema = document.Components.Schemas["DocumentedRequest"];
            Assert.That(schema.Description, Is.EqualTo("Request values."));
            Assert.That(schema.Properties["text"].Description, Is.EqualTo(flat.Description));
            Assert.That(schema.Properties["text"].MinLength, Is.EqualTo(3));
            Assert.That(schema.Required, Is.EquivalentTo(new[] { "text", "label" }));
            Assert.That(schema.Properties.ContainsKey("hidden"), Is.False);
            Assert.That(schema.Properties["label"].Description, Is.EqualTo("A label."));
            Assert.That(schema.Properties["label"].MaxLength, Is.EqualTo(10));
        }

        [TestCase("{}", "fallback")]
        [TestCase("{\"text\":null}", null)]
        [TestCase("{\"text\":\"x\"}", "x")]
        [TestCase("[\"x\"]", "x")]
        public async Task DocumentationDoesNotEnforceParameterOrResultConstraints(string json, string expected)
        {
            var context = Context("echo", json);
            context.Result = await Invoke(context);
            var envelope = JsonRpc.CreateEnvelope(context, null);
            Assert.That(context.Result, Is.EqualTo(expected));
            Assert.That(envelope["result"]?.GetValue<string>(), Is.EqualTo(expected));
        }

        [TestCase("flatten", "{}")]
        [TestCase("flatten", "{\"text\":null}")]
        [TestCase("flatten", "{\"text\":\"x\"}")]
        [TestCase("nested", "{\"values\":{}}")]
        [TestCase("nested", "{\"values\":{\"text\":null}}")]
        [TestCase("nested", "{\"values\":{\"text\":\"x\"}}")]
        public async Task DocumentationDoesNotEnforceObjectConstraints(string method, string json)
        {
            var context = Context(method, json);
            var result = (DocumentationRequest)await Invoke(context);
            Assert.That(result.Value, Is.EqualTo(json.Contains("\"x\"") ? "x" : null));
            context.Result = result;
            var serialized = JsonRpc.CreateEnvelope(context, null)["result"].AsObject();
            Assert.That(serialized.ContainsKey("text"), Is.True);
            Assert.That(serialized.ContainsKey("hidden"), Is.False);
            Assert.That(serialized.ContainsKey("value"), Is.False);
        }

        [Test]
        public void OptionalDocumentationDoesNotRelaxRuntimePresenceRules()
        {
            var document = Generate();
            foreach (var method in new[] { "required", "serializerRequired" })
            {
                var parameter = (OpenRpcContentDescriptor)document.Methods.Single(m => m.Name == "documented." + method).Params.Single();
                Assert.That(parameter.Required, Is.False);
            }
            Assert.ThrowsAsync<ArgumentException>(async () => await Invoke(Context("required", "{}")));
            Assert.ThrowsAsync<JsonException>(async () => await Invoke(Context("serializerrequired", "{}")));
        }
    }
}
