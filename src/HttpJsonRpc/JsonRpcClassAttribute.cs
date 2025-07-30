using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Class)]
    public class JsonRpcClassAttribute : Attribute
    {
        public string Name { get; }
        public string Version { get; set; }

        public JsonRpcClassAttribute(string name)
        {
            Name = name;
        }
    }
}