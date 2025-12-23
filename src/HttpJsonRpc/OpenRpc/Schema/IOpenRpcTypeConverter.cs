using System;

namespace HttpJsonRpc
{
    public interface IOpenRpcTypeConverter
    {
        bool CanConvert(OpenRpcSchemaGenerator generator, Type type);
        OpenRpcTypeInfo Convert(OpenRpcSchemaGenerator generator, OpenRpcTypeInfo info);
        string GetName(OpenRpcSchemaGenerator generator, Type type);
    }
}
