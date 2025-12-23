using System;

namespace HttpJsonRpc
{
    public class OpenRpcNullableTypeConverter : IOpenRpcTypeConverter
    {
        public bool CanConvert(OpenRpcSchemaGenerator generator, Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        public OpenRpcTypeInfo Convert(OpenRpcSchemaGenerator generator, OpenRpcTypeInfo info)
        {
            var underlyingType = Nullable.GetUnderlyingType(info.Type);
            var underlyingInfo = generator.GetTypeInfo(info.With(i =>
            {
                i.Type = underlyingType;
            }));

            return underlyingInfo.With(i =>
            {
                i.Nullable = true;
            });
        }

        public string GetName(OpenRpcSchemaGenerator generator, Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            return generator.GetName(underlyingType);
        }
    }
}
