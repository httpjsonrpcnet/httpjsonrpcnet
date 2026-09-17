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
        public OpenRpcSchemaGenerator(JsonRpcOptions options) { Options = options; }

        public string GetName(Type type)
        {
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

        public OpenRpcSchema GetSchema(Type type)
        {
            var info = GetTypeInfo(new OpenRpcTypeInfo { Type = type });
            var actual = info.Type;
            if (actual == typeof(void)) return null;
            OpenRpcSchema schema;
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
                var name = GetName(type);
                if (_SchemaTypes.TryGetValue(name, out var prior) && prior != actual)
                    throw new InvalidOperationException($"Schema name '{name}' is shared by {prior} and {actual}. Give the custom converters distinct names.");
                if (Schemas.ContainsKey(name)) return Nullable(new OpenRpcSchema { Ref = "#/components/schemas/" + name }, info.Nullable);
                _SchemaTypes[name] = actual;
                schema = new OpenRpcSchema();
                Schemas.Add(name, schema); // Register collections too: recursive collection types are valid CLR types.
                var dictionary = GetGenericInterface(actual, typeof(IDictionary<,>)) ?? GetGenericInterface(actual, typeof(IReadOnlyDictionary<,>));
                var enumerable = GetGenericInterface(actual, typeof(IEnumerable<>));
                if (dictionary != null)
                {
                    schema.AddType("object");
                    schema.AdditionalProperties = GetSchema(dictionary.GetGenericArguments()[1]);
                }
                else if (typeof(IEnumerable).IsAssignableFrom(actual))
                {
                    schema.AddType("array");
                    schema.Items = GetSchema(actual.IsArray ? actual.GetElementType() : enumerable?.GetGenericArguments()[0] ?? typeof(object));
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
                        schema.Properties.Add(property.Name, GetSchema(property.PropertyType));
                        if (property.IsRequired || (member is PropertyInfo requiredProp && requiredProp.IsRequired())) required.Add(property.Name);
                    }
                    if (required.Count > 0) schema.Required = required.ToArray();
                }
                if (info.CanRefence) schema = new OpenRpcSchema { Ref = "#/components/schemas/" + name };
            }
            return Nullable(schema, info.Nullable);
        }
        private static Type GetGenericInterface(Type type, Type definition) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == definition ? type :
            type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition);
        private static OpenRpcSchema Scalar(string type, string format = null)
        { var schema = new OpenRpcSchema { Format = format }; schema.AddType(type); return schema; }
        private static OpenRpcSchema Nullable(OpenRpcSchema schema, bool nullable)
        {
            if (!nullable) return schema;
            if (schema.Ref != null) return new OpenRpcSchema { AnyOf = new[] { schema, Scalar("null") } };
            schema.AddType("null");
            return schema;
        }
    }
}
