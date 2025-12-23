using System;

namespace HttpJsonRpc
{
    public class JsonRpcErrorFactory
    {
        public class CreateErrorArgs
        {
            public int ErrorCode { get; set; }
            public Exception Exception { get; set; }
            public JsonRpcOptions Options { get; set; }
            public JsonRpcContext Context { get; set; }
        }

        public virtual JsonRpcError CreateError(CreateErrorArgs args)
        {
            return JsonRpcError.Create(args.ErrorCode, args.Exception);
        }
    }
}
