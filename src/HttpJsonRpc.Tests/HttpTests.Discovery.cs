using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HttpJsonRpc.Tests.Fixtures.OpenRpcTransformers;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    public partial class HttpTests
    {
        [TestCase("{\"name\":\"api.add\"}")]
        [TestCase("[\"api.add\"]")]
        public async Task DiscoveryAcceptsNamedAndPositionalNameFilters(string parameters)
        {
            var document = (await Call(Request("rpc.discover", parameters)))["result"];
            Assert.That(document["methods"].AsArray().Select(m => m["name"].GetValue<string>()), Is.EqualTo(new[] { "api.add" }));
            Assert.That(OpenRpcTestSchema.Value.Evaluate(document).IsValid, Is.True);
        }

        [Test]
        public async Task DiscoveryPrefixMatchesOnlyThatGroup()
        {
            var document = (await Call(Request("rpc.discover", "{\"name\":\"api.*\"}")))["result"];
            var names = document["methods"].AsArray().Select(m => m["name"].GetValue<string>()).ToArray();
            Assert.That(names, Is.EquivalentTo(new JsonRpcClass(typeof(Api)).Methods.Values.Select(m => m.FullName)));
        }

        [TestCase("API.add")]
        [TestCase("API.*")]
        [TestCase("api.")]
        [TestCase("missing.method")]
        [TestCase("api.*dd")]
        [TestCase("")]
        public async Task DiscoveryNoMatchesReturnsAValidEmptyDocument(string name)
        {
            var document = (await Call(Request("rpc.discover", JsonSerializer.Serialize(new { name }))))["result"];
            Assert.That(document["methods"].AsArray(), Is.Empty);
            Assert.That(document["components"]["schemas"].AsObject(), Is.Empty);
            Assert.That(document["info"]["version"].GetValue<string>(), Is.EqualTo("1.0"));
            Assert.That(OpenRpcTestSchema.Value.Evaluate(document).IsValid, Is.True);
        }

        [TestCase("{}")]
        [TestCase("{\"name\":null}")]
        [TestCase("{\"name\":\"*\"}")]
        public async Task DiscoveryOmittedNullAndWildcardNamesPreserveTheFullDocument(string parameters)
        {
            var all = (await Call(Request("rpc.discover")))["result"];
            var filtered = (await Call(Request("rpc.discover", parameters)))["result"];
            Assert.That(JsonNode.DeepEquals(all, filtered), Is.True);
        }

        [Test]
        public async Task DiscoveryNameFilterCannotBypassVisibilityFilters()
        {
            Func<JsonRpcContext, JsonRpcMethod, bool> visibility = (context, method) => method.FullName != "api.add";
            JsonRpc.Options.OpenRpc.MethodFilters.Add(visibility);
            try
            {
                var document = (await Call(Request("rpc.discover", "{\"name\":\"api.add\"}")))["result"];
                Assert.That(document["methods"].AsArray(), Is.Empty);
                document = (await Call(Request("rpc.discover", "{\"name\":\"api.*\"}")))["result"];
                Assert.That(document["methods"].AsArray().Any(m => m["name"].GetValue<string>() == "api.add"), Is.False);
            }
            finally { JsonRpc.Options.OpenRpc.MethodFilters.Remove(visibility); }
        }

        [Test]
        public async Task FilteredDiscoveryIncludesDependenciesAndPreservesVersionsAndTransformers()
        {
            var methods = new System.Collections.Generic.List<string>();
            Action<OpenRpcMethodTransformerContext> methodTransformer = c =>
            {
                methods.Add(c.Method.FullName);
                c.MethodDescription.Summary = "Selected method";
            };
            Action<OpenRpcDocumentTransformerContext> documentTransformer = c =>
            {
                Assert.That(c.Methods.Select(m => m.FullName), Is.EqualTo(new[] { "discovery.contact" }));
                c.Document.Info.Description = "Filtered document";
            };
            JsonRpc.Options.OpenRpc.MethodTransformers.Add(methodTransformer);
            JsonRpc.Options.OpenRpc.DocumentTransformers.Add(documentTransformer);
            try
            {
                var document = (await Call(Request("rpc.discover", "{\"name\":\"discovery.contact\"}")))["result"];
                Assert.That(methods, Is.EqualTo(new[] { "discovery.contact" }));
                var method = document["methods"].AsArray().Single();
                Assert.That(method["x-version"].GetValue<string>(), Is.EqualTo("v1"));
                Assert.That(method["summary"].GetValue<string>(), Is.EqualTo("Selected method"));
                Assert.That(document["info"]["version"].GetValue<string>(), Is.EqualTo("1.0"));
                Assert.That(document["info"]["description"].GetValue<string>(), Is.EqualTo("Filtered document"));
                var schemas = document["components"]["schemas"].AsObject();
                var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
                Assert.That(schemas.ContainsKey(generator.GetName(typeof(Contact))), Is.True);
                Assert.That(schemas.ContainsKey(generator.GetName(typeof(Node))), Is.False);
                AssertDiscoveryReferencesResolve(document, schemas);
                Assert.That(OpenRpcTestSchema.Value.Evaluate(document).IsValid, Is.True);
            }
            finally
            {
                JsonRpc.Options.OpenRpc.MethodTransformers.Remove(methodTransformer);
                JsonRpc.Options.OpenRpc.DocumentTransformers.Remove(documentTransformer);
            }
        }

        private static void AssertDiscoveryReferencesResolve(JsonNode node, JsonObject schemas)
        {
            if (node is JsonObject obj)
            {
                if (obj.TryGetPropertyValue("$ref", out var reference))
                {
                    const string prefix = "#/components/schemas/";
                    var value = reference.GetValue<string>();
                    Assert.That(value.StartsWith(prefix, StringComparison.Ordinal), Is.True);
                    Assert.That(schemas.ContainsKey(value.Substring(prefix.Length)), Is.True, value);
                }
                foreach (var property in obj) AssertDiscoveryReferencesResolve(property.Value, schemas);
            }
            else if (node is JsonArray array)
                foreach (var item in array) AssertDiscoveryReferencesResolve(item, schemas);
        }
    }
}
