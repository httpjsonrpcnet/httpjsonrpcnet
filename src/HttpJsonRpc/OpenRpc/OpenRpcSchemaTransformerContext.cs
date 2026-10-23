using System;

namespace HttpJsonRpc
{
    public sealed class OpenRpcSchemaTransformerContext
    {
        public Type ClrType { get; }
        public Type ConvertedType { get; }
        public string ComponentName { get; }
        public OpenRpcSchema Schema { get; }
        internal OpenRpcSchemaTransformerContext(Type clrType, Type convertedType, string componentName, OpenRpcSchema schema)
        { ClrType = clrType; ConvertedType = convertedType; ComponentName = componentName; Schema = schema; }
    }
}
