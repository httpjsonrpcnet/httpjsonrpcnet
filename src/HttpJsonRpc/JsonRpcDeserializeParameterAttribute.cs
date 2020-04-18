using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Method)]
    public class JsonRpcDeserializeParameterAttribute : Attribute
    {
    }
}