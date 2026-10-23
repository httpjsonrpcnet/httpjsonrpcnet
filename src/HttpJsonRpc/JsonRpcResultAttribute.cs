using System;

namespace HttpJsonRpc
{
    /// <summary>Describes the success result of an RPC method.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class JsonRpcResultAttribute : Attribute
    {
        public string Description { get; set; }
    }
}
