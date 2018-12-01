using System;

namespace HttpJsonRpc
{
    public class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }

        public static JsonRpcError Create(int code, Exception ex = null)
        {
            JsonRpcExceptionData data = null;
            if (ex != null)
            {
                data = new JsonRpcExceptionData
                {
                    Message = ex.Message,
                    StackTrace = ex.StackTrace
                };
            }

            return Create(code, data);
        }

        public static JsonRpcError Create(int code, object data = null)
        {
            return new JsonRpcError
            {
                Code = code,
                Message = JsonRpcErrorCodes.GetMessage(code),
                Data = data
            };
        }
    }
}