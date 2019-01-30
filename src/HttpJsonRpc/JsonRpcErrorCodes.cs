namespace HttpJsonRpc
{
    public static class JsonRpcErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        //Custom codes
        public const int Unauthorized = 1;

        public static string GetMessage(int code)
        {
            switch (code)
            {
                case ParseError:
                    return "Parse error";
                case InvalidRequest:
                    return "Invalid JsonRpcRequest";
                case MethodNotFound:
                    return "Method not found";
                case InvalidParams:
                    return "Invalid params";
                case InternalError:
                    return "Internal error";
                case Unauthorized:
                    return "Unauthorized";
                default:
                    return null;
            }
        }
    }
}