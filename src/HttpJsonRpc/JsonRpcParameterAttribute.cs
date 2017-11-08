using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class JsonRpcParameterAttribute : Attribute
    {
        public string Name { get; set; }
        public bool Ignore { get; set; }
    }
}