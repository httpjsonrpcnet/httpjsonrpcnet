using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace HttpJsonRpc
{
    public class JsonRpcClass
    {
        private readonly string _Key;
        public string Key => _Key;

        private readonly string _Name;
        public string Name => _Name;

        private readonly string _Version;
        public string Version => _Version;

        private readonly Type _ClassType;
        public Type ClassType => _ClassType;

        private readonly ImmutableDictionary<string, JsonRpcMethod> _Methods;
        public ImmutableDictionary<string, JsonRpcMethod> Methods => _Methods;

        private readonly MethodInfo _ReceivedRequestMethod;
        public MethodInfo ReceivedRequestMethod => _ReceivedRequestMethod;

        private readonly MethodInfo _CompletedRequestMethod;
        public MethodInfo CompletedRequestMethod => _CompletedRequestMethod;

        private readonly MethodInfo _DeserializeParameterMethod;
        public MethodInfo DeserializeParameterMethod => _DeserializeParameterMethod;

        private readonly MethodInfo _GetSerializerOptionsMethod;
        public MethodInfo GetSerializerOptionsMethod => _GetSerializerOptionsMethod;

        public JsonRpcClass(Type type)
        {
            _ClassType = type;

            var classAttribute = _ClassType.GetCustomAttribute<JsonRpcClassAttribute>();
            _Name = classAttribute.Name ?? type.Name;
            _Version = classAttribute.Version;
            _Key = (string.IsNullOrWhiteSpace(_Version) ? _Name : $"{_Version}:{_Name}").ToLowerInvariant();

            var methodInfos = _ClassType.GetMethods().ToArray();
            var rpcMethods = methodInfos.Where(i => i.IsDefined(typeof(JsonRpcMethodAttribute))).ToArray();
            var methodsBuilder = ImmutableDictionary.CreateBuilder<string, JsonRpcMethod>();
            foreach (var m in rpcMethods)
            {
                var method = new JsonRpcMethod(this, m);
                methodsBuilder.Add(method.Name.ToLowerInvariant(), method);
            }
            _Methods = methodsBuilder.ToImmutable();

            _ReceivedRequestMethod = methodInfos.FirstOrDefault(m => m.IsDefined(typeof(JsonRpcReceivedRequestAttribute)));
            _CompletedRequestMethod = methodInfos.FirstOrDefault(m => m.IsDefined(typeof(JsonRpcCompletedRequestAttribute)));
            _DeserializeParameterMethod = methodInfos.FirstOrDefault(m => m.IsDefined(typeof(JsonRpcDeserializeParameterAttribute)));
            _GetSerializerOptionsMethod = methodInfos.FirstOrDefault(m => m.IsDefined(typeof(JsonRpcGetSerializerOptionsAttribute)));
        }
    }
}