using System;
using System.Collections.Generic;
using System.Reflection;

namespace HttpJsonRpc
{
    public class JsonRpcClass
    {
        public string Name { get; set; }
        public Type ClassType { get; set; }
        public Dictionary<string, JsonRpcMethod> Methods { get; } = new Dictionary<string, JsonRpcMethod>();

        public MethodInfo ReceivedRequestMethod { get; set; }
        public MethodInfo DeserializeParameterMethod { get; set; }
    }
}