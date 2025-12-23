using System;
using System.Collections;

namespace HttpJsonRpc
{
    public class OpenRpcDictionaryTypeConverter : IOpenRpcTypeConverter
    {
        public bool CanConvert(OpenRpcSchemaGenerator generator, Type type)
        {
            return typeof(IDictionary).IsAssignableFrom(type);
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
