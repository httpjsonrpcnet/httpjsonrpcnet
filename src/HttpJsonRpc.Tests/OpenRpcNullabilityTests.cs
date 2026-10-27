using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using HttpJsonRpc.Tests.Fixtures.OpenRpcNullability;
using Json.Schema;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public class OpenRpcNullabilityTests
    {
        private static readonly JsonSerializerOptions Serializer = new OpenRpcApi().GetSerializerOptions(null, null);
        private static OpenRpcDocument Generate(JsonRpcOptions options = null) => OpenRpcDocumentGenerator.Generate(
            options ?? new JsonRpcOptions(), null, new JsonRpcClass(typeof(NullableApi)).Methods.Values.ToArray());
        private static OpenRpcContentDescriptor Result(OpenRpcDocument document, string name) =>
            (OpenRpcContentDescriptor)document.Methods.Single(m => m.Name == "nullable." + name).Result;
        private static OpenRpcContentDescriptor[] Parameters(OpenRpcDocument document, string name) =>
            document.Methods.Single(m => m.Name == "nullable." + name).Params.Cast<OpenRpcContentDescriptor>().ToArray();
        private static bool Accepts(OpenRpcSchema schema, OpenRpcComponents components, string json)
        {
            // JsonSchema.Net recognizes recursive definitions in the standard Draft 7
            // container. Relocate OpenRPC components and their references for instance validation.
            var root = new JsonObject
            {
                ["$schema"] = "http://json-schema.org/draft-07/schema#",
                ["allOf"] = new JsonArray(JsonNode.Parse(JsonSerializer.Serialize(schema, Serializer))),
                ["definitions"] = JsonNode.Parse(JsonSerializer.Serialize(components.Schemas, Serializer))
            };
            return JsonSchema.FromText(root.ToJsonString().Replace("#/components/schemas/", "#/definitions/")).Evaluate(JsonNode.Parse(json)).IsValid;
        }
        private static OpenRpcSchema ContractSchema(OpenRpcDocument document) =>
            document.Components.Schemas[new OpenRpcSchemaGenerator(new JsonRpcOptions()).GetName(typeof(Contract))];

        [Test]
        public void RequiredAndNullableAreIndependentAndDoNotChangeBindingMetadata()
        {
            var document = Generate();
            var schema = ContractSchema(document);
            Assert.That(schema.Required, Is.EquivalentTo(new[] { "requiredValue", "requiredNull", "presenceOnly" }));
            foreach (var name in new[] { "requiredNull", "optionalNull" })
                Assert.That(Accepts(schema.Properties[name], document.Components, "null"), Is.True, name);
            foreach (var name in new[] { "requiredValue", "optionalValue" })
                Assert.That(Accepts(schema.Properties[name], document.Components, "null"), Is.False, name);
            Assert.That(Accepts(schema, document.Components, "{\"requiredValue\":\"\",\"requiredNull\":null,\"presenceOnly\":null}"), Is.True);
            Assert.That(Accepts(schema, document.Components, "{\"requiredValue\":\"\",\"presenceOnly\":null}"), Is.False);
            Assert.That(Parameters(document, "override")[0].Required, Is.False);
            Assert.That(Parameters(document, "requiredDefault")[0].Required, Is.True);
            var methods = new JsonRpcClass(typeof(NullableApi)).Methods.Values;
            Assert.That(methods.Single(m => m.Name == "override").Parameters[0].Optional, Is.False);
            Assert.That(methods.Single(m => m.Name == "requiredDefault").Parameters[0].Optional, Is.True);
        }

        [Test]
        public void MetadataDescribesValuesReferencesArraysAndDictionaryValues()
        {
            var document = Generate();
            var properties = ContractSchema(document).Properties;
            foreach (var name in new[] { "age", "alias", "entries", "next", "tone", "presenceOnly", "optionalNames" })
                Assert.That(Accepts(properties[name], document.Components, "null"), Is.True, name);
            foreach (var name in new[] { "count", "name", "names", "aliases", "anything", "strictNumber", "strictReference" })
                Assert.That(Accepts(properties[name], document.Components, "null"), Is.False, name);
            Assert.That(Accepts(properties["entries"], document.Components, "[null,\"a\"]"), Is.True);
            Assert.That(Accepts(properties["labels"], document.Components, "{\"a\":null}"), Is.True);
            Assert.That(Accepts(properties["tone"], document.Components, "999"), Is.False);
            Assert.That(Accepts(properties["strictNumber"], document.Components, "12"), Is.True);
            Assert.That(Accepts(properties["anything"], document.Components, "42"), Is.True);
        }

        [Test]
        public void SharedClrTypesKeepDistinctNestedNullabilityAndRecursiveReferences()
        {
            var document = Generate();
            var properties = ContractSchema(document).Properties;
            Assert.That(Accepts(properties["names"], document.Components, "[null]"), Is.False);
            Assert.That(Accepts(properties["aliases"], document.Components, "[null]"), Is.True);
            Assert.That(Accepts(properties["optionalNames"], document.Components, "[null]"), Is.False);
            Assert.That(Accepts(properties["strictBox"], document.Components, "{\"value\":null}"), Is.False);
            Assert.That(Accepts(properties["nullableBox"], document.Components, "{\"value\":null,\"next\":{\"value\":null}}"), Is.True);
            Assert.That(Accepts(properties["strictBox"], document.Components, "{\"next\":{\"value\":null}}"), Is.False);
            Assert.That(Accepts(properties["strictReference"], document.Components, "{\"value\":null}"), Is.True);
        }

        [Test]
        public void ParametersAsyncResultsAndFlattenedGenericPropertiesKeepMetadata()
        {
            var document = Generate();
            var parameters = Parameters(document, "find");
            Assert.That(parameters.Select(p => p.Required), Is.EqualTo(new[] { true, true, true, false }));
            Assert.That(Accepts(parameters[0].Schema, document.Components, "null"), Is.False);
            Assert.That(Accepts(parameters[1].Schema, document.Components, "null"), Is.True);
            Assert.That(Accepts(parameters[2].Schema, document.Components, "null"), Is.True);
            Assert.That(Accepts(Result(document, "find").Schema, document.Components, "null"), Is.True);
            Assert.That(Accepts(Result(document, "strictResult").Schema, document.Components, "null"), Is.False);
            Assert.That(Accepts(Result(document, "nullableResult").Schema, document.Components, "null"), Is.True);
            Assert.That(Accepts(Result(document, "read").Schema, document.Components, "{\"value\":null}"), Is.True);
            Assert.That(Accepts(Result(document, "read").Schema, document.Components, "null"), Is.False);
            Assert.That(Accepts(Parameters(document, "flatten").Single(p => p.Name == "value").Schema, document.Components, "null"), Is.True);
        }

        [Test]
        public void InheritedGenericMetadataAndLegacyFallbackAreSupported()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            var derived = generator.GetSchema(typeof(DerivedBox));
            var list = generator.GetSchema(typeof(NullableList));
            var legacy = generator.GetSchema(typeof(LegacyContract));
            var components = new OpenRpcComponents { Schemas = generator.Schemas };
            Assert.That(Accepts(derived, components, "{\"value\":null}"), Is.True);
            Assert.That(Accepts(list, components, "[null]"), Is.True);
            Assert.That(Accepts(legacy, components, "{\"name\":null}"), Is.False);
            Assert.That(Accepts(legacy, components, "{\"alias\":null}"), Is.True);
        }

        [Test]
        public void NestedGenericArraysNullableStructsFieldsAndShadowedMembersUseTheirOwnMetadata()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            var nested = generator.GetSchema(typeof(NestedContract));
            var derived = generator.GetSchema(typeof(NonNullableDerived));
            var components = new OpenRpcComponents { Schemas = generator.Schemas };
            Assert.That(Accepts(nested, components, "{\"values\":{\"a\":null,\"b\":[[null,\"x\"]]}}"), Is.True);
            Assert.That(Accepts(nested, components, "{\"strict\":{\"b\":[[null]]}}"), Is.False);
            Assert.That(Accepts(nested, components, "{\"values\":{\"b\":[null]}}"), Is.False);
            Assert.That(Accepts(nested, components, "{\"pair\":null}"), Is.True);
            Assert.That(Accepts(nested, components, "{\"pair\":{\"first\":1,\"second\":null}}"), Is.True);
            Assert.That(Accepts(nested, components, "{\"pair\":{\"first\":null}}"), Is.False);
            Assert.That(Accepts(nested, components, "{\"field\":null,\"readOnly\":null}"), Is.True);
            Assert.That(generator.Schemas[generator.GetName(typeof(NestedContract))].Properties.ContainsKey("hidden"), Is.False);
            Assert.That(Accepts(derived, components, "{\"value\":null}"), Is.False);
            Assert.That(Accepts(derived, components, "{\"inherited\":null}"), Is.True);
        }

        [Test]
        public void ComponentContractsAreIndependentOfDiscoveryOrder()
        {
            var options = new JsonRpcOptions();
            var methods = new JsonRpcClass(typeof(NullableApi)).Methods.Values.ToArray();
            var first = OpenRpcDocumentGenerator.Generate(options, null, methods);
            var second = OpenRpcDocumentGenerator.Generate(options, null, Enumerable.Reverse(methods).ToArray());
            Assert.That(second.Components.Schemas.Keys, Is.EquivalentTo(first.Components.Schemas.Keys));
            foreach (var key in first.Components.Schemas.Keys)
                Assert.That(JsonSerializer.Serialize(second.Components.Schemas[key], Serializer),
                    Is.EqualTo(JsonSerializer.Serialize(first.Components.Schemas[key], Serializer)), key);
        }

        [Test]
        public void TransformersRunAfterOverridesAndDocumentRemainsOpenRpcCompatible()
        {
            var options = new JsonRpcOptions();
            options.OpenRpc.Info.Title = "Nullable API";
            options.OpenRpc.Info.Version = "1";
            options.OpenRpc.SchemaUsageTransformers.Add(c =>
            {
                if (c.JsonName != "optionalValue") return;
                Assert.That(c.Schema.Type, Is.EqualTo("string"));
                c.Schema = new OpenRpcSchema();
            });
            var document = Generate(options);
            Assert.That(Accepts(ContractSchema(document).Properties["optionalValue"], document.Components, "null"), Is.True);
            var json = JsonNode.Parse(JsonSerializer.Serialize(document, Serializer));
            Assert.That(OpenRpcTestSchema.Value.Evaluate(json).IsValid, Is.True);
            Assert.That(json.ToJsonString(), Does.Not.Contain("\"nullable\":"));
        }
    }
}
