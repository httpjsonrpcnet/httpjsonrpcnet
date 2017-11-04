namespace HttpJsonRpc
{
    public static class ErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        //Custom codes
        public const int Unauthorized = 1;
        public const int ActionNotAllowed = 2;
        public const int ExecutionError = 3;

        public static string GetMessage(int code)
        {
            switch (code)
            {
                case ParseError:
                    return "Parse error";
                case InvalidRequest:
                    return "Invalid Request";
                case MethodNotFound:
                    return "Method not found";
                case InvalidParams:
                    return "Invalid params";
                case InternalError:
                    return "Internal error";
                default:
                    return null;
            }
        }
    }
}