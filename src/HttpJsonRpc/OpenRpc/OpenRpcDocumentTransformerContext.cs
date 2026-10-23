using System.Collections.Generic;

namespace HttpJsonRpc
{
    public sealed class OpenRpcDocumentTransformerContext
    {
        public JsonRpcContext RequestContext { get; }
        public IReadOnlyList<JsonRpcMethod> Methods { get; }
        public OpenRpcDocument Document { get; }
        internal OpenRpcDocumentTransformerContext(JsonRpcContext requestContext, IReadOnlyList<JsonRpcMethod> methods, OpenRpcDocument document)
        { RequestContext = requestContext; Methods = methods; Document = document; }
    }
}
