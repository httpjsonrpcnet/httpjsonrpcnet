using System;
using System.IO;

namespace HttpJsonRpc
{
    public class OpenRpcStreamTypeConverter : IOpenRpcTypeConverter
    {
        public bool CanConvert(OpenRpcSchemaGenerator generator, Type type)
        {
            return type == typeof(JsonRpcStreamResult) || typeof(Stream).IsAssignableFrom(type);
        }

        public OpenRpcTypeInfo Convert(OpenRpcSchemaGenerator generator, OpenRpcTypeInfo info)
        {
            return info.With(i =>
            {
                i.Type = typeof(Stream);
                i.CanRefence = true;
                i.IsOpaque = true;
            });
        }

        public string GetName(OpenRpcSchemaGenerator generator, Type type)
        {
            return "Stream";
        }
    }
}
