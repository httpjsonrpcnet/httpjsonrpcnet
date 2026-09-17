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
            var error = JsonRpcError.Create(args.ErrorCode);
            if (args.Options.IncludeExceptionMessagesInErrors)
                error.Message = args.Exception?.Message ?? error.Message;
            if (args.Options.IncludeStackTraceInErrors)
                error.Data = new JsonRpcExceptionData { StackTrace = args.Exception?.StackTrace };
            return error;
        }
    }
}
