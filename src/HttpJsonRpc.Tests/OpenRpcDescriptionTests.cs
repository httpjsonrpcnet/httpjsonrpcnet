using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public class OpenRpcDescriptionTests
    {
        private static OpenRpcDocument Generate(JsonRpcOptions options) => OpenRpcDocumentGenerator.Generate(
            options, null, new JsonRpcClass(typeof(ContactApi)).Methods.Values.ToArray());

        [Test]
        public void AttributesDescribeMethodsDescriptorsAndNestedSchemasWithoutTransformers()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.Info.Title = "Contact API";
            options.OpenRpc.Info.Version = "1";
            var document = Generate(options);
            var create = document.Methods.Single(m => m.Name == "describedContact.create");
            Assert.That(create.Description, Is.EqualTo("Creates a contact."));
            Assert.That(((OpenRpcContentDescriptor)create.Params[0]).Description, Is.EqualTo("Input values."));
            Assert.That(((OpenRpcContentDescriptor)create.Result).Description, Is.EqualTo("The newly created contact."));
            var find = document.Methods.Single(m => m.Name == "describedContact.find");
            Assert.That(((OpenRpcContentDescriptor)find.Params[0]).Name, Is.EqualTo("contact_values"));
            Assert.That(((OpenRpcContentDescriptor)find.Params[0]).Description, Is.EqualTo("The contact to create."));
            Assert.That(((OpenRpcContentDescriptor)find.Result).Description, Is.EqualTo("The matching contact."));
            Assert.That(document.Methods.Single(m => m.Name == "describedContact.complete").Result, Is.Null);
            var generator = new OpenRpcSchemaGenerator(options);
            var contact = document.Components.Schemas[generator.GetName(typeof(Contact))];
            var address = document.Components.Schemas[generator.GetName(typeof(Address))];
            Assert.That(contact.Description, Is.EqualTo("A contact record."));
            Assert.That(address.Description, Is.EqualTo("A postal address."));
            Assert.That(contact.Properties["billing_address"].Description, Is.EqualTo("Where invoices are sent."));
            Assert.That(contact.Properties["shipping"].Description, Is.EqualTo("Where deliveries are sent."));
            Assert.That(contact.Properties["billing_address"].AllOf[0].Ref,
                Is.EqualTo(contact.Properties["shipping"].AllOf[0].Ref));
            Assert.That(address.Properties["city"].Description, Is.EqualTo("City or town."));
            Assert.That(contact.Properties.ContainsKey("hidden"), Is.False);
            Assert.That(contact.Properties["internalLabel"].Description, Is.Null);
            var json = JsonNode.Parse(JsonSerializer.Serialize(document, new OpenRpcApi().GetSerializerOptions(null, null)));
            Assert.That(OpenRpcTestSchema.Value.Evaluate(json).IsValid, Is.True);
            Assert.That(json["components"]["schemas"][generator.GetName(typeof(Contact))]["properties"]["internalLabel"].AsObject().ContainsKey("description"), Is.False);
        }

        [Test]
        public void TransformersSeeAndCanOverrideOrClearAttributeDescriptions()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.SchemaTransformers.Add(c =>
            {
                if (c.ConvertedType != typeof(Address)) return;
                Assert.That(c.Schema.Description, Is.EqualTo("A postal address."));
                c.Schema.Description = "Shared address override.";
            });
            options.OpenRpc.SchemaUsageTransformers.Add(c =>
            {
                if (c.Kind != OpenRpcSchemaUsageKind.Property) return;
                if (c.JsonName == "billing_address")
                {
                    Assert.That(c.Schema.Description, Is.EqualTo("Where invoices are sent."));
                    c.Schema.Description = "Billing override.";
                }
                if (c.JsonName == "shipping") c.Schema.Description = null;
            });
            options.OpenRpc.MethodTransformers.Add(c =>
            {
                if (c.Method.Name != "create") return;
                Assert.That(c.Result.Description, Is.EqualTo("The newly created contact."));
                c.Result.Description = "Result override.";
                c.Parameters.Values.Single().Description = "Parameter override.";
            });
            var document = Generate(options);
            var generator = new OpenRpcSchemaGenerator(options);
            var contact = document.Components.Schemas[generator.GetName(typeof(Contact))];
            Assert.That(contact.Properties["billing_address"].Description, Is.EqualTo("Billing override."));
            Assert.That(contact.Properties["shipping"].Description, Is.Null);
            Assert.That(document.Components.Schemas[generator.GetName(typeof(Address))].Description, Is.EqualTo("Shared address override."));
            var create = document.Methods.Single(m => m.Name == "describedContact.create");
            Assert.That(((OpenRpcContentDescriptor)create.Result).Description, Is.EqualTo("Result override."));
            Assert.That(((OpenRpcContentDescriptor)create.Params[0]).Description, Is.EqualTo("Parameter override."));
        }

        [Test]
        public void SchemaCopiesKeepDescriptionsAndWrapperOrderDoesNotChangeTypeDocumentation()
        {
            foreach (var firstType in new[] { typeof(Contact), typeof(Task<Contact>) })
            {
                var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
                generator.GetSchema(firstType);
                generator.GetSchema(typeof(Task<Contact>));
                generator.GetSchema(typeof(Contact));
                var contact = generator.Schemas[generator.GetName(typeof(Contact))];
                var copy = contact.Copy();
                Assert.That(copy.Description, Is.EqualTo("A contact record."));
                Assert.That(copy.Properties["billing_address"].Description, Is.EqualTo("Where invoices are sent."));
                copy.Properties["billing_address"].Description = "Copy only";
                Assert.That(contact.Properties["billing_address"].Description, Is.EqualTo("Where invoices are sent."));
            }
        }

        [Test]
        public void InheritedAndShadowedMembersUseTheSerializedContract()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            generator.GetSchema(typeof(DerivedContact));
            var schema = generator.Schemas[generator.GetName(typeof(DerivedContact))];
            Assert.That(schema.Description, Is.EqualTo("A contact record."));
            Assert.That(schema.Properties["billing_address"].Description, Is.EqualTo("Where invoices are sent."));
            Assert.That(schema.Properties["shipping"].Description, Is.EqualTo("Overridden shipping location."));
        }

        [Test]
        public void StandardDescriptionAttributesAreNotImported()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            generator.GetSchema(typeof(UiOnlyType));
            var schema = generator.Schemas[generator.GetName(typeof(UiOnlyType))];
            Assert.That(schema.Description, Is.Null);
            Assert.That(schema.Properties["value"].Description, Is.Null);
        }
    }
}
