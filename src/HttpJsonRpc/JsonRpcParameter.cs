using System;
using System.Reflection;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpcParameter
    {
        [JsonIgnore]
        public ParameterInfo ParameterInfo { get; }
        [JsonIgnore]
        public PropertyInfo PropertyInfo { get; }
        public string Name { get; }
        public string Description { get; }
        public Type ClrType { get; }
        public string JsonType { get; }
        public bool Optional { get; }

        public JsonRpcParameter(string name, string description, Type clrType, bool optional)
            : this(name, description, clrType, optional, null, null)
        {
        }

        internal JsonRpcParameter(string name, string description, Type clrType, bool optional, ParameterInfo parameterInfo, PropertyInfo propertyInfo)
        {
            ParameterInfo = parameterInfo;
            PropertyInfo = propertyInfo;
            Name = name;
            Description = description;
            ClrType = clrType;
            JsonType = JsonTypeMap.GetJsonType(clrType);
            Optional = optional;
        }
    }
}