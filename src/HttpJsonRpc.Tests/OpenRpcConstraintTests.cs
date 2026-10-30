using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using HttpJsonRpc.Tests.Fixtures.OpenRpcConstraints;
using Json.Schema;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public class OpenRpcConstraintTests
    {
        private static readonly JsonSerializerOptions Serializer = new OpenRpcApi().GetSerializerOptions(null, null);
        private static OpenRpcDocument Generate(JsonRpcOptions options = null)
        {
            options = options ?? new JsonRpcOptions();
            options.OpenRpc.Info.Title = "Constraint API";
            options.OpenRpc.Info.Version = "1";
            return OpenRpcDocumentGenerator.Generate(options, null, new JsonRpcClass(typeof(ConstraintApi)).Methods.Values.ToArray());
        }
        private static OpenRpcSchema Request(OpenRpcDocument document) => document.Components.Schemas[
            new OpenRpcSchemaGenerator(new JsonRpcOptions()).GetName(typeof(ConstraintRequest))];
        private static bool Accepts(OpenRpcDocument document, OpenRpcSchema schema, string json)
        {
            // Use Draft 7 definitions so the validator recognizes referenced schemas.
            var root = new JsonObject
            {
                ["$schema"] = "http://json-schema.org/draft-07/schema#",
                ["allOf"] = new JsonArray(JsonNode.Parse(JsonSerializer.Serialize(schema, Serializer))),
                ["definitions"] = JsonNode.Parse(JsonSerializer.Serialize(document.Components.Schemas, Serializer))
            };
            return JsonSchema.FromText(root.ToJsonString().Replace("#/components/schemas/", "#/definitions/"))
                .Evaluate(JsonNode.Parse(json)).IsValid;
        }

        [Test]
        public void ConstraintsValidateStringsNumbersAndArraysWithoutChangingSharedSchemas()
        {
            var document = Generate();
            var properties = Request(document).Properties;
            Assert.That(Accepts(document, properties["email"], "\"alex@example.com\""), Is.True);
            Assert.That(Accepts(document, properties["email"], "\"invalid\""), Is.False);
            Assert.That(Accepts(document, properties["email"], "\"abcdefghijklmnopqrstuvwxyz@example.com\""), Is.False);
            Assert.That(Accepts(document, properties["amount"], "0.5"), Is.True);
            foreach (var value in new[] { "0", "10", "-1", "11", "0.3" })
                Assert.That(Accepts(document, properties["amount"], value), Is.False, value);
            Assert.That(Accepts(document, properties["tags"], "[\"a\",\"b\"]"), Is.True);
            foreach (var value in new[] { "[]", "[\"a\",\"a\"]", "[\"a\",\"b\",\"c\"]" })
                Assert.That(Accepts(document, properties["tags"], value), Is.False, value);
            Assert.That(Accepts(document, properties["unrestricted"], "[\"a\",\"a\",\"a\"]"), Is.True);
            Assert.That(Accepts(document, properties["empty"], "[]"), Is.True);
            Assert.That(Accepts(document, properties["empty"], "[\"a\"]"), Is.False);
            Assert.That(Accepts(document, properties["zero"], "null"), Is.True);
            Assert.That(Accepts(document, properties["zero"], "0"), Is.True);
            Assert.That(Accepts(document, properties["zero"], "1"), Is.False);
            Assert.That(Accepts(document, properties["emptyText"], "\"\""), Is.True);
            Assert.That(Accepts(document, properties["emptyText"], "\"a\""), Is.False);
            Assert.That(Accepts(document, properties["externalAnnotations"], "\"anything\""), Is.True);
        }

        [Test]
        public void ParametersFlattenedPropertiesResultsAndTypesApplyBeforeTransformers()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.SchemaTransformers.Add(c =>
            {
                if (c.ConvertedType == typeof(LimitedNames))
                {
                    Assert.That(c.Schema.MaxItems, Is.EqualTo(3));
                    c.Schema.MaxItems = 4;
                }
            });
            options.OpenRpc.SchemaUsageTransformers.Add(c =>
            {
                if (c.JsonName == "code")
                {
                    Assert.That(c.Schema.MinLength, Is.EqualTo(2));
                    c.Schema.MinLength = 1;
                }
            });
            var document = Generate(options);
            var find = document.Methods.Single(m => m.Name == "constraints.find");
            Assert.That(((OpenRpcContentDescriptor)find.Params[0]).Schema.MinLength, Is.EqualTo(1));
            var result = ((OpenRpcContentDescriptor)find.Result).Schema;
            Assert.That(result.Format, Is.EqualTo("custom-code"));
            Assert.That(Accepts(document, result, "\"AB\""), Is.True);
            Assert.That(Accepts(document, result, "\"ab\""), Is.False);
            Assert.That(Accepts(document, result, "\"A\""), Is.False);
            var flat = document.Methods.Single(m => m.Name == "constraints.flatten").Params.Cast<OpenRpcContentDescriptor>();
            Assert.That(flat.Single(p => p.Name == "email").Schema.MaxLength, Is.EqualTo(30));
            var names = ((OpenRpcContentDescriptor)document.Methods.Single(m => m.Name == "constraints.names").Result).Schema;
            Assert.That(Accepts(document, names, "[]"), Is.False);
            Assert.That(Accepts(document, names, "[\"a\",\"b\"]"), Is.True);
            Assert.That(Accepts(document, names, "[\"a\",\"b\",\"c\"]"), Is.False);
            Assert.That(document.Components.Schemas[new OpenRpcSchemaGenerator(options).GetName(typeof(LimitedNames))].MaxItems, Is.EqualTo(4));
        }

        [Test]
        public void SerializationAndCopiesPreserveExplicitZeroFalseAndOmitUnsetConstraints()
        {
            var document = Generate();
            var properties = Request(document).Properties;
            foreach (var property in properties.Values)
                Assert.That(JsonSerializer.Serialize(property.Copy(), Serializer), Is.EqualTo(JsonSerializer.Serialize(property, Serializer)));
            var empty = JsonNode.Parse(JsonSerializer.Serialize(properties["empty"], Serializer));
            Assert.That(empty["minItems"].GetValue<int>(), Is.Zero);
            Assert.That(empty["maxItems"].GetValue<int>(), Is.Zero);
            Assert.That(empty["uniqueItems"].GetValue<bool>(), Is.False);
            var unset = JsonNode.Parse(JsonSerializer.Serialize(properties["externalAnnotations"], Serializer)).AsObject();
            Assert.That(unset.ContainsKey("minLength"), Is.False);
            Assert.That(unset.ContainsKey("pattern"), Is.False);
            var zero = JsonNode.Parse(JsonSerializer.Serialize(properties["zero"], Serializer));
            Assert.That(zero["minimum"].GetValue<double>(), Is.Zero);
            Assert.That(zero["maximum"].GetValue<double>(), Is.Zero);
            Assert.That(OpenRpcTestSchema.Value.Evaluate(JsonNode.Parse(JsonSerializer.Serialize(document, Serializer))).IsValid, Is.True);
        }

        [TestCase("MinLength", -1d)]
        [TestCase("MaxLength", -1d)]
        [TestCase("MinItems", -1d)]
        [TestCase("MaxItems", -1d)]
        [TestCase("MultipleOf", 0d)]
        [TestCase("MultipleOf", -1d)]
        [TestCase("Minimum", double.NaN)]
        [TestCase("Maximum", double.PositiveInfinity)]
        [TestCase("ExclusiveMinimum", double.NegativeInfinity)]
        [TestCase("ExclusiveMaximum", double.NaN)]
        public void InvalidKeywordValuesFailDiscovery(string name, double value)
        {
            var attribute = new OpenRpcParameterAttribute();
            var property = typeof(OpenRpcParameterAttribute).GetProperty(name);
            property.SetValue(attribute, Convert.ChangeType(value, property.PropertyType));
            Assert.Throws<InvalidOperationException>(() => attribute.Constraints.Apply(new OpenRpcSchema()));
        }
    }
}
