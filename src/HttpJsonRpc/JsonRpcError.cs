using System;

namespace HttpJsonRpc
{
    public class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }

        public static JsonRpcError Create(int code, Exception ex = null, bool includeStackTrace = true)
        {
            var e = new JsonRpcError
            {
                Code = code
            };

            if (ex == null)
            {
                e.Message = JsonRpcErrorCodes.GetMessage(code);
            }
            else
            {
                e.Message = ex.Message;

                e.Data = new JsonRpcExceptionData
                {
                    StackTrace = includeStackTrace ? ex.StackTrace : null
                };
            }

            return e;
        }
    }
}