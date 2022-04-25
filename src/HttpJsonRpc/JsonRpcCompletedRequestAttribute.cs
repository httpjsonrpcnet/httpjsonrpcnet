using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class JsonRpcCompletedRequestAttribute : Attribute
    {
    }
}