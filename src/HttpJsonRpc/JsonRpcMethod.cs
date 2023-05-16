using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace HttpJsonRpc
{
    public class JsonRpcMethod
    {
        private readonly string _Name;
        public string Name => _Name;

        private readonly string _Description;
        public string Description => _Description;

        private readonly ImmutableDictionary<string, JsonRpcParameter> _Parameters;
        public ImmutableDictionary<string, JsonRpcParameter> Parameters => _Parameters;

        private readonly MethodInfo _MethodInfo;
        [JsonIgnore]
        public MethodInfo MethodInfo => _MethodInfo;

        private readonly JsonRpcClass _ParentClass;
        [JsonIgnore]
        public JsonRpcClass ParentClass => _ParentClass;

        public JsonRpcMethod(JsonRpcClass parent, MethodInfo info)
        {
            _ParentClass = parent;
            _MethodInfo = info;

            var attribute = _MethodInfo.GetCustomAttribute<JsonRpcMethodAttribute>();

            _Name = attribute.Name ?? _MethodInfo.Name;
            var asyncIndex = _Name.LastIndexOf("Async", StringComparison.Ordinal);
            if (asyncIndex > -1)
            {
                _Name = _Name.Remove(asyncIndex);
            }

            _Description = attribute.Description;

            var parametersBuilder = ImmutableDictionary.CreateBuilder<string, JsonRpcParameter>();
            foreach (var parameterInfo in _MethodInfo.GetParameters())
            {
                var parameterAttribute = parameterInfo.GetCustomAttribute<JsonRpcParameterAttribute>();
                if (parameterAttribute?.Ignore ?? false) continue;

                var parameter = new JsonRpcParameter(parameterInfo);
                parametersBuilder.Add(parameter.Name, parameter);
            }

            _Parameters = parametersBuilder.ToImmutable();
        }
    }
}