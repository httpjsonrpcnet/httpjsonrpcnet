using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NUnit.Framework;
using HttpJsonRpc.Tests.Fixtures.OpenRpcTransformers;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public class OpenRpcTransformerTests
    {
        private static JsonRpcMethod[] Methods() => new JsonRpcClass(typeof(ContactApi)).Methods.Values.OrderBy(m => m.Name).ToArray();
        private static OpenRpcDocument Generate(JsonRpcOptions options) => OpenRpcDocumentGenerator.Generate(options, null, Methods());

        [Test]
        public void HooksRunInOrderAndExposeReflectionAndDescriptorMappings()
        {
            var options = new JsonRpcOptions();
            var events = new List<string>();
            var usages = new List<OpenRpcSchemaUsageTransformerContext>();
            options.OpenRpc.SchemaTransformers.Add(c =>
            {
                if (c.ConvertedType == typeof(string)) c.Schema.Format = "first";
                if (c.ConvertedType == typeof(Contact)) events.Add("contact");
            });
            options.OpenRpc.SchemaTransformers.Add(c =>
            {
                if (c.ConvertedType == typeof(string)) { Assert.That(c.Schema.Format, Is.EqualTo("first")); c.Schema.Format = "second"; }
            });
            options.OpenRpc.SchemaUsageTransformers.Add(c =>
            {
                usages.Add(c);
                if (c.Kind == OpenRpcSchemaUsageKind.Property && c.JsonName == "birthday")
                { Assert.That(c.Member.Name, Is.EqualTo(nameof(Contact.BirthDate))); c.Schema.Format = "date"; }
            });
            options.OpenRpc.MethodTransformers.Add(c =>
            {
                Assert.That(c.Parameters.Keys, Is.EquivalentTo(c.Method.Parameters));
                foreach (var pair in c.Parameters) Assert.That(c.MethodDescription.Params, Does.Contain(pair.Value));
                Assert.That(c.MethodDescription.Result, Is.SameAs(c.Result));
                c.MethodDescription.Summary = "first";
                events.Add(c.Method.Name);
            });
            options.OpenRpc.MethodTransformers.Add(c =>
            { Assert.That(c.MethodDescription.Summary, Is.EqualTo("first")); c.MethodDescription.Summary = "second"; });
            options.OpenRpc.DocumentTransformers.Add(c =>
            {
                Assert.That(c.Methods.Count, Is.EqualTo(3));
                Assert.That(c.Document.Methods.All(m => m.Summary == "second"), Is.True);
                events.Add("document");
                c.Document.Info.Description = "first";
            });
            options.OpenRpc.DocumentTransformers.Add(c =>
            { Assert.That(c.Document.Info.Description, Is.EqualTo("first")); c.Document.Info.Description = "second"; });
            var document = Generate(options);
            Assert.That(events.Count(e => e == "contact"), Is.EqualTo(1));
            Assert.That(events.Last(), Is.EqualTo("document"));
            var contact = document.Components.Schemas[new OpenRpcSchemaGenerator(options).GetName(typeof(Contact))];
            Assert.That(contact.Properties["birthday"].Format, Is.EqualTo("date"));
            Assert.That(contact.Properties["name"].Format, Is.EqualTo("second"));
            var named = usages.Single(c => c.Kind == OpenRpcSchemaUsageKind.Parameter && c.Method.Name == "create");
            Assert.That(named.Parameter.Name, Is.EqualTo("contact"));
            var dto = usages.Single(c => c.Kind == OpenRpcSchemaUsageKind.Parameter && c.Method.Name == "search");
            Assert.That(dto.Member.Name, Is.EqualTo(nameof(SearchRequest.Query)));
            Assert.That(dto.JsonName, Is.EqualTo("query_text"));
            var result = usages.Single(c => c.Kind == OpenRpcSchemaUsageKind.Result && c.Method.Name == "create");
            Assert.That(result.Parameter, Is.EqualTo(result.Method.MethodInfo.ReturnParameter));
            Assert.That(result.ClrType, Is.EqualTo(typeof(Task<Contact>)));
            Assert.That(usages.Any(c => c.Kind == OpenRpcSchemaUsageKind.ArrayItem), Is.True);
            Assert.That(usages.Any(c => c.Kind == OpenRpcSchemaUsageKind.DictionaryValue), Is.True);
        }

        [Test]
        public void SharedReferencesHaveIndependentUsageWrappersAndRemainValid()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.Info.Title = "Contacts";
            options.OpenRpc.Info.Version = "1";
            options.OpenRpc.SchemaUsageTransformers.Add(c =>
            {
                if (c.Kind == OpenRpcSchemaUsageKind.Parameter && c.Method.Name == "create")
                    c.Schema.Required = new[] { "name" };
            });
            var document = Generate(options);
            var first = ((OpenRpcContentDescriptor)document.Methods.Single(m => m.Name == "contact.create").Params[0]).Schema;
            var second = ((OpenRpcContentDescriptor)document.Methods.Single(m => m.Name == "contact.echo").Params[0]).Schema;
            Assert.That(first.AllOf[0].Ref, Is.EqualTo(second.AllOf[0].Ref));
            Assert.That(first.Required, Is.EqualTo(new[] { "name" }));
            Assert.That(second.Required, Is.Null);
            Assert.That(document.Components.Schemas[new OpenRpcSchemaGenerator(options).GetName(typeof(Contact))].Required, Is.Null);
            var json = JsonNode.Parse(JsonSerializer.Serialize(document, new OpenRpcApi().GetSerializerOptions(null, null)));
            Assert.That(OpenRpcTestSchema.Value.Evaluate(json).IsValid, Is.True);
        }

        [Test]
        public void RecursiveComponentsAreTransformedOnceAndReferencesTerminate()
        {
            var options = new JsonRpcOptions();
            var count = 0;
            options.OpenRpc.SchemaTransformers.Add(c => { if (c.ConvertedType == typeof(Node)) count++; });
            options.OpenRpc.SchemaUsageTransformers.Add(c => { });
            var generator = new OpenRpcSchemaGenerator(options);
            generator.GetSchema(typeof(Node));
            generator.GetSchema(typeof(Node));
            Assert.That(count, Is.EqualTo(1));
            var schema = generator.Schemas[generator.GetName(typeof(Node))];
            Assert.That(schema.Properties["next"].AllOf[0].Ref, Is.EqualTo("#/components/schemas/" + generator.GetName(typeof(Node))));
        }

        [Test]
        public async Task DocumentMutationsAreIsolatedAcrossConcurrentCalls()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.Info.Title = "Original";
            options.OpenRpc.Info.Contact = new OpenRpcContact { Name = "Original" };
            options.OpenRpc.Info.License = new OpenRpcLicense { Name = "Original" };
            options.OpenRpc.DocumentTransformers.Add(c =>
            {
                Assert.That(c.Document.Info.Title, Is.EqualTo("Original"));
                Assert.That(c.Document.Info.Contact.Name, Is.EqualTo("Original"));
                Assert.That(c.Document.Info.License.Name, Is.EqualTo("Original"));
                c.Document.Info.Title = "Changed";
                c.Document.Info.Contact.Name = "Changed";
                c.Document.Info.License.Name = "Changed";
            });
            var documents = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Generate(options))));
            Assert.That(options.OpenRpc.Info.Title, Is.EqualTo("Original"));
            Assert.That(options.OpenRpc.Info.Contact.Name, Is.EqualTo("Original"));
            Assert.That(options.OpenRpc.Info.License.Name, Is.EqualTo("Original"));
            Assert.That(documents[0].Components.Schemas, Is.Not.SameAs(documents[1].Components.Schemas));
        }

        [TestCase("schema"), TestCase("usage"), TestCase("method"), TestCase("document")]
        public void TransformerFailuresPropagate(string stage)
        {
            var options = new JsonRpcOptions();
            var failure = new InvalidOperationException("Transformer failed");
            if (stage == "schema") options.OpenRpc.SchemaTransformers.Add(c => throw failure);
            if (stage == "usage") options.OpenRpc.SchemaUsageTransformers.Add(c => throw failure);
            if (stage == "method") options.OpenRpc.MethodTransformers.Add(c => throw failure);
            if (stage == "document") options.OpenRpc.DocumentTransformers.Add(c => throw failure);
            Assert.That(Assert.Throws<InvalidOperationException>(() => Generate(options)), Is.SameAs(failure));
        }

        [Test]
        public void UsageReplacementIsVisibleToLaterCallbacks()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.SchemaUsageTransformers.Add(c =>
            {
                if (c.Kind != OpenRpcSchemaUsageKind.Property || c.JsonName != "birthday") return;
                c.Schema = new OpenRpcSchema { Format = "date" };
                c.Schema.AddType("string");
            });
            options.OpenRpc.SchemaUsageTransformers.Add(c =>
            {
                if (c.Kind == OpenRpcSchemaUsageKind.Property && c.JsonName == "birthday")
                    Assert.That(c.Schema.Format, Is.EqualTo("date"));
            });
            var generator = new OpenRpcSchemaGenerator(options);
            generator.GetSchema(typeof(Contact));
            Assert.That(generator.Schemas[generator.GetName(typeof(Contact))].Properties["birthday"].Format, Is.EqualTo("date"));
        }

        [Test]
        public void TypeTransformersReceiveWrapperAndConvertedTypes()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.TypeConverters.Insert(0, new UnitTests.WrapperConverter());
            options.OpenRpc.SchemaTransformers.Add(c =>
            {
                Assert.That(c.ClrType, Is.EqualTo(typeof(UnitTests.Wrapper<Guid>)));
                Assert.That(c.ConvertedType, Is.EqualTo(typeof(Guid)));
                Assert.That(c.ComponentName, Is.Null);
                Assert.That(c.Schema.Format, Is.EqualTo("uuid"));
                c.Schema.Format = "custom-id";
            });
            Assert.That(new OpenRpcSchemaGenerator(options).GetSchema(typeof(UnitTests.Wrapper<Guid>)).Format, Is.EqualTo("custom-id"));
        }

    }
}
