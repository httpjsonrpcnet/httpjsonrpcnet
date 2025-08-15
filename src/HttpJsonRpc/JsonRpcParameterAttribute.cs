using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public class JsonRpcParameterAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Ignore { get; set; }
    }
}