using System;

namespace HttpJsonRpc
{
    /// <summary>Describes an OpenRPC method without registering or renaming it.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class OpenRpcMethodAttribute : Attribute
    {
        public string Description { get; set; }
    }
}
