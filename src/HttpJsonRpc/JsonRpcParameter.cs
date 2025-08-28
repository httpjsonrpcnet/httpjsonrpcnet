using System;

namespace HttpJsonRpc
{
    public class JsonRpcParameter
    {
        public string Name { get; }
        public string Description { get; }
        public Type ClrType { get; }
        public string JsonType { get; }
        public bool Optional { get; }

        public JsonRpcParameter(string name, string description, Type clrType, bool optional)
        {
            Name = name;
            Description = description;
            ClrType = clrType;
            JsonType = JsonTypeMap.GetJsonType(clrType);
            Optional = optional;
        }
    }
}