using System;
using System.Reflection;

namespace HttpJsonRpc
{
    public sealed class OpenRpcSchemaUsageTransformerContext
    {
        public OpenRpcSchemaUsageKind Kind { get; }
        public Type ClrType { get; }
        public Type ContainingType { get; }
        public string JsonName { get; }
        public MemberInfo Member { get; }
        public ParameterInfo Parameter { get; }
        public JsonRpcMethod Method { get; }
        public OpenRpcSchema Schema { get; set; }
        internal OpenRpcSchemaUsageTransformerContext(OpenRpcSchemaUsageKind kind, Type clrType, OpenRpcSchema schema,
            Type containingType, string jsonName, MemberInfo member, ParameterInfo parameter, JsonRpcMethod method)
        {
            Kind = kind; ClrType = clrType; Schema = schema; ContainingType = containingType;
            JsonName = jsonName; Member = member; Parameter = parameter; Method = method;
        }
    }
}
