using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public class OpenRpcCompatibilityTests
    {
        private JsonSchema _Schema;

        [OneTimeSetUp]
        public void LoadSchema()
        {
            _Schema = OpenRpcTestSchema.Value;
        }

        private static JsonNode Serialize(OpenRpcDocument document)
        {
            var options = new OpenRpcApi().GetSerializerOptions(null, null);
            return JsonNode.Parse(JsonSerializer.Serialize(document, options));
        }

        private void AssertValid(JsonNode document)
        {
            var result = _Schema.Evaluate(document, new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.That(result.IsValid, Is.True, JsonSerializer.Serialize(result));
        }

        [TestCase("1.3.0")]
        [TestCase("1.5.0")]
        [TestCase("1x4x1")]
        public void IncompatibleVersionIsRejected(string version)
        {
            var document = Serialize(new OpenRpcDocument
            {
                Info = new OpenRpcInfo { Title = "Example API", Version = "2.0" },
                Methods = new OpenRpcMethod[0]
            });
            AssertValid(document);
            document["openrpc"] = version;
            Assert.That(_Schema.Evaluate(document).IsValid, Is.False);
        }

        [Test]
        public void PopulatedTagsAndExamplePairingsUseStandardFields()
        {
            var tag = new OpenRpcTag { Name = "contacts", Description = "Contact operations." };
            var pairing = new OpenRpcExamplePairing
            {
                Name = "createContact",
                Params = new[] { new OpenRpcExample { Name = "name", Value = "Alex" } },
                Result = new OpenRpcExample { Name = "id", Value = 42 }
            };
            var stringSchema = new OpenRpcSchema();
            stringSchema.AddType("string");
            var integerSchema = new OpenRpcSchema();
            integerSchema.AddType("integer");
            var document = Serialize(new OpenRpcDocument
            {
                Info = new OpenRpcInfo { Title = "Example API", Version = "2.0" },
                Methods = new[]
                {
                    new OpenRpcMethod
                    {
                        Name = "contact.create",
                        Params = new object[] { new OpenRpcContentDescriptor { Name = "name", Required = true, Schema = stringSchema } },
                        Result = new OpenRpcContentDescriptor { Name = "id", Schema = integerSchema },
                        Tags = new object[] { new Dictionary<string, string> { ["$ref"] = "#/components/tags/contacts" } },
                        Examples = new object[] { new Dictionary<string, string> { ["$ref"] = "#/components/examplePairings/createContact" } }
                    }
                },
                Components = new OpenRpcComponents
                {
                    Tags = new Dictionary<string, OpenRpcTag> { ["contacts"] = tag },
                    ExamplePairings = new Dictionary<string, OpenRpcExamplePairing> { ["createContact"] = pairing }
                }
            });

            Assert.That(document["openrpc"].GetValue<string>(), Is.EqualTo("1.4.1"));
            Assert.That(document["components"]["examplePairings"]["createContact"]["name"].GetValue<string>(), Is.EqualTo("createContact"));
            Assert.That(document["components"].AsObject().ContainsKey("examplePairingObjects"), Is.False);
            Assert.That(typeof(OpenRpcTag).GetProperty("Summary"), Is.Null);
            AssertValid(document);

            // Validate populated objects inline too: a meta-schema validates reference syntax,
            // but does not follow document references to check the target's contents.
            document["methods"][0]["tags"] = new JsonArray(document["components"]["tags"]["contacts"].DeepClone());
            document["methods"][0]["examples"] = new JsonArray(document["components"]["examplePairings"]["createContact"].DeepClone());
            AssertValid(document);
            document["methods"][0]["tags"][0]["summary"] = "Not a standard tag field";
            Assert.That(_Schema.Evaluate(document).IsValid, Is.False);
        }
    }

    internal static class OpenRpcTestSchema
    {
        public static JsonSchema Value { get; }

        static OpenRpcTestSchema()
        {
            var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas");
            SchemaRegistry.RegisterNewSpecVersion(new Uri("https://meta.json-schema.tools/"), SpecVersion.Draft7);
            SchemaRegistry.Global.Register(new Uri("https://meta.json-schema.tools/"), JsonSchema.FromFile(Path.Combine(directory, "json-schema.json")));
            Value = JsonSchema.FromFile(Path.Combine(directory, "openrpc.json"));
        }
    }

}
