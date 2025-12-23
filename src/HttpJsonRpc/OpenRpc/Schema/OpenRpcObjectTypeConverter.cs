using System;

namespace HttpJsonRpc
{
    public class OpenRpcObjectTypeConverter : IOpenRpcTypeConverter
    {
        public bool CanConvert(OpenRpcSchemaGenerator generator, Type type)
        {
            return type == typeof(object);
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
            return "object";
        }
    }
}
