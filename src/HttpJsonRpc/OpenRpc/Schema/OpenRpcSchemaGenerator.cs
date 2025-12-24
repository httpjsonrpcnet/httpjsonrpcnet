using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class OpenRpcSchemaGenerator
    {
        public JsonRpcOptions Options { get; }
        public Dictionary<string, OpenRpcSchema> Schemas { get; } = new Dictionary<string, OpenRpcSchema>();

        public OpenRpcSchemaGenerator(JsonRpcOptions options)
        {
            Options = options;
        }

        public string GetName(Type type)
        {
            var converter = Options.OpenRpc.TypeConverters
                .FirstOrDefault(i => i.CanConvert(this, type));

            if (converter != null)
            {
                return converter.GetName(this, type);
            }

            if (!type.IsGenericType) return type.Name;

            var parts = new List<string>
            {
                type.Name.Substring(0, type.Name.IndexOf('`'))
            };

            parts.AddRange(type.GetGenericArguments().Select(GetName));

            return string.Join("_", parts);
        }

        public OpenRpcTypeInfo GetTypeInfo(OpenRpcTypeInfo info)
        {
            return Options.OpenRpc.TypeConverters
                .FirstOrDefault(i => i.CanConvert(this, info.Type))
                ?.Convert(this, info) ?? info;
        }

        private OpenRpcTypeInfo GetTypeInfo(Type type)
        {
            return GetTypeInfo(new OpenRpcTypeInfo { Type = type });
        }

        private Dictionary<string, OpenRpcSchema> GetProperties(Type type)
        {
            var properties = new Dictionary<string, OpenRpcSchema>();
            foreach (var prop in type.GetProperties())
            {
                var ignoreAttribute = prop.GetCustomAttribute<JsonIgnoreAttribute>();
                if (ignoreAttribute != null && ignoreAttribute.Condition == JsonIgnoreCondition.Always)
                {
                    continue;
                }

                var nameAttribute = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                var name = nameAttribute?.Name ?? prop.Name;
                if (Options.SerializerOptions.PropertyNamingPolicy != null)
                {
                    name = Options.SerializerOptions.PropertyNamingPolicy.ConvertName(name);
                }

                properties[name] = GetSchema(prop.PropertyType);
            }

            return properties;
        }

        public OpenRpcSchema GetSchema(Type type)
        {
            var name = GetName(type);
            if (name != null && Schemas.ContainsKey(name))
            {
                return new OpenRpcSchema
                {
                    Ref = $"#/components/schemas/{name}"
                };
            }

            var info = GetTypeInfo(type);
            var jsonType = JsonTypeMap.GetJsonType(info.Type);
            if (jsonType is null)
            {
                return null;
            }

            OpenRpcSchema schema = null;

            switch (jsonType)
            {
                case "array":
                    var itemType = info.Type.IsArray
                        ? info.Type.GetElementType()
                        : info.Type.GetGenericArguments().FirstOrDefault() ?? typeof(object);

                    schema = new OpenRpcSchema
                    {
                        Items = GetSchema(itemType)
                    };
                    schema.AddType("array");

                    break;
                case "object":
                    var objectSchema = new OpenRpcSchema
                    {
                        Nullable = info.Nullable
                    };
                    objectSchema.AddType("object");

                    if (info.CanRefence)
                    {
                        // Add to Schemas BEFORE recursing to prevent infinite recursion
                        Schemas.Add(name, objectSchema);
                    }

                    if (!info.IsOpaque)
                    {
                        objectSchema.Properties = GetProperties(info.Type);
                    }

                    if (info.CanRefence)
                    {
                        schema = new OpenRpcSchema
                        {
                            Ref = $"#/components/schemas/{name}"
                        };
                    }
                    else
                    {
                        schema = objectSchema;
                    }

                    break;
                default:
                    schema = new OpenRpcSchema();
                    schema.AddType(jsonType);
                    break;
            }

            if (info?.Nullable ?? false)
            {
                schema.AddType("null");
            }

            return schema;
        }
    }
}
