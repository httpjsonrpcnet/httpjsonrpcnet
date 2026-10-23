using System.Collections.Generic;

namespace HttpJsonRpc
{
    public sealed class OpenRpcMethodTransformerContext
    {
        public JsonRpcContext RequestContext { get; }
        public JsonRpcMethod Method { get; }
        public OpenRpcMethod MethodDescription { get; }
        public IReadOnlyDictionary<JsonRpcParameter, OpenRpcContentDescriptor> Parameters { get; }
        public OpenRpcContentDescriptor Result { get; }
        internal OpenRpcMethodTransformerContext(JsonRpcContext requestContext, JsonRpcMethod method,
            OpenRpcMethod description, IReadOnlyDictionary<JsonRpcParameter, OpenRpcContentDescriptor> parameters,
            OpenRpcContentDescriptor result)
        { RequestContext = requestContext; Method = method; MethodDescription = description; Parameters = parameters; Result = result; }
    }
}
