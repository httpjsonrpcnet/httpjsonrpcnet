using System;
using System.Collections.Generic;

namespace HttpJsonRpc
{
    public static class JsonTypeMap
    {
        private static Dictionary<Type, string> Types { get; } = new Dictionary<Type, string>();

        static JsonTypeMap()
        {
            Types.Add(typeof(byte), "number");
            Types.Add(typeof(byte?), "number");
            Types.Add(typeof(sbyte), "number");
            Types.Add(typeof(sbyte?), "number");
            Types.Add(typeof(short), "number");
            Types.Add(typeof(short?), "number");
            Types.Add(typeof(ushort), "number");
            Types.Add(typeof(ushort?), "number");
            Types.Add(typeof(int), "number");
            Types.Add(typeof(int?), "number");
            Types.Add(typeof(uint), "number");
            Types.Add(typeof(uint?), "number");
            Types.Add(typeof(long), "number");
            Types.Add(typeof(long?), "number");
            Types.Add(typeof(ulong), "number");
            Types.Add(typeof(ulong?), "number");
            Types.Add(typeof(float), "number");
            Types.Add(typeof(float?), "number");
            Types.Add(typeof(double), "number");
            Types.Add(typeof(double?), "number");
            Types.Add(typeof(decimal), "number");
            Types.Add(typeof(decimal?), "number");
            Types.Add(typeof(bool), "boolean");
            Types.Add(typeof(bool?), "boolean");
            Types.Add(typeof(char), "string");
            Types.Add(typeof(char?), "string");
            Types.Add(typeof(DateTime), "string");
            Types.Add(typeof(DateTime?), "string");
            Types.Add(typeof(DateTimeOffset), "string");
            Types.Add(typeof(DateTimeOffset?), "string");
            Types.Add(typeof(TimeSpan), "string");
            Types.Add(typeof(TimeSpan?), "string");
            Types.Add(typeof(string), "string");
        }

        public static string GetJsonType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            Types.TryGetValue(type, out var jsonType);

            return jsonType ?? type.Name;
        }
    }
}