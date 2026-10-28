using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace HttpJsonRpc
{
    public class OpenRpcSchemaGenerator
    {
        public JsonRpcOptions Options { get; }
        public Dictionary<string, OpenRpcSchema> Schemas { get; } = new Dictionary<string, OpenRpcSchema>();
        private readonly Dictionary<string, Type> _SchemaTypes = new Dictionary<string, Type>();
        private readonly Action<OpenRpcSchemaTransformerContext>[] _SchemaTransformers;
        private readonly Action<OpenRpcSchemaUsageTransformerContext>[] _UsageTransformers;
        public OpenRpcSchemaGenerator(JsonRpcOptions options)
        {
            Options = options;
            _SchemaTransformers = options.OpenRpc.SchemaTransformers.ToArray();
            _UsageTransformers = options.OpenRpc.SchemaUsageTransformers.ToArray();
        }

        internal OpenRpcSchema GetUsageSchema(Type type, OpenRpcSchemaUsageKind kind,
            Type containingType = null, string jsonName = null, MemberInfo member = null,
            ParameterInfo parameter = null, JsonRpcMethod method = null, OpenRpcNullabilityInfo nullability = null, OpenRpcNullabilityInfo containing = null)
        {
            nullability = nullability ?? OpenRpcNullabilityReader.Read(type, member, parameter, containing);
            var schema = GetSchema(type, nullability);
            if (schema == null) return null;
            // Parameters and results have their own content descriptors. Nested properties
            // need their documentation on the local schema instead.
            var description = kind == OpenRpcSchemaUsageKind.Property
                ? member?.GetCustomAttribute<JsonRpcParameterAttribute>(true)?.Description : null;
            var parameterAttribute = member?.GetCustomAttribute<JsonRpcParameterAttribute>(true)
                ?? parameter?.GetCustomAttribute<JsonRpcParameterAttribute>(true);
            var nullableOverride = kind == OpenRpcSchemaUsageKind.Result
                ? member?.GetCustomAttribute<JsonRpcResultAttribute>(true)?.NullableOverride : parameterAttribute?.NullableOverride;
            if (nullableOverride.HasValue) schema = ApplyNullability(schema, nullableOverride.Value);
            var constraints = kind == OpenRpcSchemaUsageKind.Result
                ? member?.GetCustomAttribute<JsonRpcResultAttribute>(true)?.Constraints : parameterAttribute?.Constraints;
            if (description == null && constraints?.HasValues != true && _UsageTransformers.Length == 0) return schema;
            schema = schema.Copy();
            // Draft 7 ignores siblings of $ref. Each usage gets an independent wrapper.
            if (schema.Ref != null) schema = new OpenRpcSchema { AllOf = new[] { schema } };
            if (description != null) schema.Description = description;
            constraints?.Apply(schema);
            var context = new OpenRpcSchemaUsageTransformerContext(kind, type, schema, containingType, jsonName, member, parameter, method);
            foreach (var transformer in _UsageTransformers)
            {
                transformer(context);
                if (context.Schema == null) throw new InvalidOperationException("A schema usage transformer cannot remove its schema.");
            }
            return context.Schema;
        }

        private void TransformSchema(Type type, Type convertedType, string name, OpenRpcSchema schema)
        {
            var attribute = convertedType.GetCustomAttribute<JsonRpcSchemaAttribute>(true);
            schema.Description = attribute?.Description;
            attribute?.Constraints.Apply(schema);
            var context = new OpenRpcSchemaTransformerContext(type, convertedType, name, schema);
            foreach (var transformer in _SchemaTransformers) transformer(context);
        }

        public string GetName(Type type)
        {
            var name = type.GetCustomAttribute<JsonRpcSchemaAttribute>(true)?.Name;
            if (name != null)
            {
                if (!Regex.IsMatch(name, @"\A[a-zA-Z0-9_.-]+\z"))
                    throw new InvalidOperationException($"Schema name '{name}' on {type} must contain only letters, digits, underscores, periods or hyphens and cannot be empty.");
                return name;
            }
            var converter = Options.OpenRpc.TypeConverters.FirstOrDefault(c => c.CanConvert(this, type));
            if (converter != null) return converter.GetName(this, type);
            return GetTypeIdentity(type);
        }
        private static string GetTypeIdentity(Type type)
        {
            if (type.IsArray) return GetTypeIdentity(type.GetElementType()) + "_Array" + type.GetArrayRank();
            var name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
            name = Regex.Replace(name ?? type.Name, @"`\d+", "");
            if (type.IsGenericType) name += "_" + string.Join("_", type.GetGenericArguments().Select(GetTypeIdentity));
            return Regex.Replace(name, @"[^a-zA-Z0-9_.-]", "_");
        }
        public OpenRpcTypeInfo GetTypeInfo(OpenRpcTypeInfo info) => Options.OpenRpc.TypeConverters
            .FirstOrDefault(c => c.CanConvert(this, info.Type))?.Convert(this, info) ?? info;

        public OpenRpcSchema GetSchema(Type type) => GetSchema(type, OpenRpcNullabilityReader.Unknown(type));

        private OpenRpcSchema GetSchema(Type type, OpenRpcNullabilityInfo nullability)
        {
            var info = GetTypeInfo(new OpenRpcTypeInfo { Type = type });
            var actual = info.Type;
            // Follow built-in wrapper conversions without confusing Task nullability with its result.
            while (nullability.Type != actual && nullability.Arguments.Length == 1 &&
                (System.Nullable.GetUnderlyingType(nullability.Type) != null ||
                (nullability.Type.IsGenericType && (nullability.Type.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>) ||
                    nullability.Type.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>)))))
                nullability = nullability.Arguments[0];
            // A custom wire-type conversion preserves the outer usage annotation. Its new
            // nested shape has no corresponding CLR metadata and remains the converter's responsibility.
            if (nullability.Type != actual) nullability = OpenRpcNullabilityReader.Unknown(actual).WithNullable(nullability.Nullable);
            var nullable = info.Nullable || nullability.Nullable == true;
            if (actual == typeof(void)) return null;
            OpenRpcSchema schema;
            var transformed = false;
            if (info.IsOpaque)
            {
                schema = new OpenRpcSchema();
                // Object and dictionaries can contain any JSON value through custom converters.
                if (actual != typeof(object)) schema.AddType("object");
            }
            else if (actual.IsEnum)
            {
                var values = System.Enum.GetValues(actual).Cast<object>().Select(v => JsonSerializer.SerializeToElement(v, Options.SerializerOptions)).ToArray();
                schema = Scalar(values.FirstOrDefault().ValueKind == JsonValueKind.String ? "string" : "integer");
                schema.Enum = values.Cast<object>().ToArray();
            }
            else if (actual == typeof(Guid)) schema = Scalar("string", "uuid");
            else if (actual == typeof(byte[])) schema = Scalar("string", "byte");
            else if (actual == typeof(DateTime) || actual == typeof(DateTimeOffset)) schema = Scalar("string", "date-time");
            else if (actual == typeof(string) || actual == typeof(char) || actual == typeof(TimeSpan) || actual == typeof(Uri)) schema = Scalar("string");
            else if (actual == typeof(bool)) schema = Scalar("boolean");
            else if (actual == typeof(float) || actual == typeof(double) || actual == typeof(decimal)) schema = Scalar("number");
            else if (actual.IsPrimitive) schema = Scalar("integer");
            else
            {
                // Fail explicitly rather than silently reusing another CLR type's schema.
                var name = GetName(type) + nullability.ComponentSuffix;
                if (_SchemaTypes.TryGetValue(name, out var prior) && prior != actual)
                    throw new InvalidOperationException($"Schema name '{name}' is shared by {prior} and {actual}. Give the schema attributes or custom converters distinct names.");
                if (Schemas.ContainsKey(name)) return Nullable(new OpenRpcSchema { Ref = "#/components/schemas/" + name }, nullable);
                _SchemaTypes[name] = actual;
                schema = new OpenRpcSchema();
                Schemas.Add(name, schema); // Register collections too: recursive collection types are valid CLR types.
                var dictionary = GetGenericInterface(actual, typeof(IDictionary<,>)) ?? GetGenericInterface(actual, typeof(IReadOnlyDictionary<,>));
                var enumerable = GetGenericInterface(actual, typeof(IEnumerable<>));
                if (dictionary != null)
                {
                    schema.AddType("object");
                    schema.AdditionalProperties = GetUsageSchema(dictionary.GetGenericArguments()[1], OpenRpcSchemaUsageKind.DictionaryValue, actual,
                        nullability: OpenRpcNullabilityReader.CollectionArgument(nullability, dictionary, 1));
                }
                else if (typeof(IEnumerable).IsAssignableFrom(actual))
                {
                    schema.AddType("array");
                    schema.Items = GetUsageSchema(actual.IsArray ? actual.GetElementType() : enumerable?.GetGenericArguments()[0] ?? typeof(object), OpenRpcSchemaUsageKind.ArrayItem, actual,
                        nullability: actual.IsArray ? nullability.Arguments[0] : enumerable == null ? null : OpenRpcNullabilityReader.CollectionArgument(nullability, enumerable, 0));
                }
                else
                {
                    schema.AddType("object");
                    schema.Properties = new Dictionary<string, OpenRpcSchema>();
                    var required = new List<string>();
                    // Let System.Text.Json resolve inheritance, shadowing, naming, and field inclusion.
                    // Custom wire shapes still require an OpenRPC converter; here we describe the
                    // default object contract rather than attempting to infer converter output.
                    var contractOptions = new JsonSerializerOptions(Options.SerializerOptions);
                    contractOptions.Converters.Clear();
                    var contract = new DefaultJsonTypeInfoResolver().GetTypeInfo(actual, contractOptions);
                    foreach (var property in contract.Properties)
                    {
                        if (property.Get == null) continue;
                        var member = property.AttributeProvider as MemberInfo;
                        if (member is PropertyInfo prop && Options.SerializerOptions.IgnoreReadOnlyProperties && prop.SetMethod == null) continue;
                        if (member is FieldInfo field && Options.SerializerOptions.IgnoreReadOnlyFields && field.IsInitOnly) continue;
                        if (property.IsExtensionData)
                        {
                            schema.AdditionalProperties = new OpenRpcSchema();
                            continue;
                        }
                        schema.Properties.Add(property.Name, GetUsageSchema(property.PropertyType, OpenRpcSchemaUsageKind.Property, actual, property.Name, member, containing: nullability));
                        var requiredOverride = member?.GetCustomAttribute<JsonRpcParameterAttribute>(true)?.RequiredOverride;
                        if (requiredOverride ?? (property.IsRequired || (member is PropertyInfo requiredProp && requiredProp.IsRequired()))) required.Add(property.Name);
                    }
                    if (required.Count > 0) schema.Required = required.ToArray();
                }
                TransformSchema(type, actual, name, schema);
                transformed = true;
                if (info.CanRefence) schema = new OpenRpcSchema { Ref = "#/components/schemas/" + name };
            }
            if (!transformed) TransformSchema(type, actual, null, schema);
            schema = Nullable(schema, nullable);
            if (!nullable && nullability.Nullable == false && schema.Type == null && schema.Ref == null)
                schema = ApplyNullability(schema, false);
            return schema;
        }
        private static Type GetGenericInterface(Type type, Type definition) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == definition ? type :
            type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition);
        private static OpenRpcSchema Scalar(string type, string format = null)
        { var schema = new OpenRpcSchema { Format = format }; schema.AddType(type); return schema; }
        private static OpenRpcSchema ApplyNullability(OpenRpcSchema schema, bool nullable)
        {
            if (nullable) return Nullable(schema.Copy(), true);
            if (schema.Ref == null && ((schema.Type is string scalar && scalar != "null") ||
                (schema.Type is string[] types && types.Any(t => t != "null"))))
            {
                var copy = schema.Copy();
                copy.RemoveType("null");
                return copy;
            }
            // This also excludes null from referenced, enum, and composed schemas without changing a shared definition.
            return new OpenRpcSchema { AllOf = new[] { schema }, Not = Scalar("null") };
        }
        private static OpenRpcSchema Nullable(OpenRpcSchema schema, bool nullable)
        {
            if (!nullable) return schema;
            if (schema.Ref != null || schema.Enum != null || schema.AllOf != null || schema.AnyOf != null || schema.Not != null)
                return new OpenRpcSchema { AnyOf = new[] { schema, Scalar("null") } };
            if (schema.Type == null) return schema; // An unconstrained schema already permits null.
            schema.AddType("null");
            return schema;
        }
    }
}
