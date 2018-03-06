using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Class)]
    public class JsonRpcClassAttribute : Attribute
    {
        public string Name { get; }

        public JsonRpcClassAttribute(string name)
        {
            Name = name;
        }
    }
}