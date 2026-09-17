using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HttpJsonRpc
{
    public class OpenRpcDictionaryTypeConverter : IOpenRpcTypeConverter
    {
        public bool CanConvert(OpenRpcSchemaGenerator generator, Type type)
        {
            return typeof(IDictionary).IsAssignableFrom(type) && !type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        public OpenRpcTypeInfo Convert(OpenRpcSchemaGenerator generator, OpenRpcTypeInfo info)
        {
            return info.With(i =>
            {
                i.CanRefence = false;
                i.IsOpaque = true;
            });
        }

        public string GetName(OpenRpcSchemaGenerator generator, Type type)
        {
            return "Dictionary";
        }
    }
}
