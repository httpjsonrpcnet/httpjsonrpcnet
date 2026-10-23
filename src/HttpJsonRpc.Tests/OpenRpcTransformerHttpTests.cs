using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    public partial class HttpTests
    {
        [Test]
        public async Task DiscoveryTransformersRespectFiltersAndReceiveRequestContext()
        {
            var options = JsonRpc.Options.OpenRpc;
            JsonRpcContext observedContext = null;
            Func<JsonRpcContext, JsonRpcMethod, bool> filter = (c, m) => m.FullName != "api.nil";
            Action<OpenRpcMethodTransformerContext> methodTransformer = c =>
            {
                Assert.That(c.Method.FullName, Is.Not.EqualTo("api.nil"));
                Assert.That(c.RequestContext, Is.Not.Null);
                observedContext = c.RequestContext;
                c.MethodDescription.Summary = "Customized method";
            };
            Action<OpenRpcDocumentTransformerContext> documentTransformer = c =>
            {
                Assert.That(c.RequestContext, Is.SameAs(observedContext));
                Assert.That(c.Methods.Any(m => m.FullName == "api.nil"), Is.False);
                c.Document.Servers = new[] { new OpenRpcServer { Name = "example", Url = "https://example.test/rpc" } };
            };
            options.MethodFilters.Add(filter);
            options.MethodTransformers.Add(methodTransformer);
            options.DocumentTransformers.Add(documentTransformer);
            try
            {
                var response = await Call(Request("rpc.discover"));
                Assert.That(response["error"], Is.Null);
                Assert.That(response["result"]["methods"].AsArray().All(m => m["summary"].GetValue<string>() == "Customized method"), Is.True);
                Assert.That(response["result"]["servers"][0]["url"].GetValue<string>(), Is.EqualTo("https://example.test/rpc"));
                Assert.That(OpenRpcTestSchema.Value.Evaluate(response["result"]).IsValid, Is.True);
            }
            finally
            {
                options.MethodFilters.Remove(filter);
                options.MethodTransformers.Remove(methodTransformer);
                options.DocumentTransformers.Remove(documentTransformer);
            }
        }

        [Test]
        public async Task FailingDocumentTransformerReturnsRpcErrorInsteadOfPartialDocument()
        {
            Action<OpenRpcDocumentTransformerContext> transformer = c => throw new InvalidOperationException("private transformer failure");
            JsonRpc.Options.OpenRpc.DocumentTransformers.Add(transformer);
            try
            {
                var response = await Call(Request("rpc.discover"));
                Assert.That(response.AsObject().ContainsKey("result"), Is.False);
                Assert.That(response["error"]["code"].GetValue<int>(), Is.EqualTo(-32603));
                Assert.That(response["error"]["message"].GetValue<string>(), Is.EqualTo("Internal error"));
            }
            finally { JsonRpc.Options.OpenRpc.DocumentTransformers.Remove(transformer); }
        }
    }
}
