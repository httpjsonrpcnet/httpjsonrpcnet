using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Method)]
    public class JsonRpcMethodAttribute : Attribute
    {
        public string Name { get; }

        public JsonRpcMethodAttribute()
        {
        }

        public JsonRpcMethodAttribute(string name)
        {
            Name = name;
        }
    }
}