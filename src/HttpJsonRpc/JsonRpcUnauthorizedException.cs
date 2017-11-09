using System;
using System.Diagnostics;

namespace HttpJsonRpc
{
    public class JsonRpcUnauthorizedException : Exception
    {
        public JsonRpcUnauthorizedException()
        {
        }

        public JsonRpcUnauthorizedException(string message) : base(message)
        {
        }

        public static void Throw()
        {
            var caller = new StackTrace().GetFrame(1).GetMethod();
            var message = $"Unauthorized: Access denied while calling {caller.DeclaringType.Name}.{caller.Name}.";
            throw new JsonRpcUnauthorizedException(message);
        }
    }
}