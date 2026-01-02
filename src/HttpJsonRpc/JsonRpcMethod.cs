using System;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpcMethod
    {
        private readonly string _Name;
        public string Name => _Name;

        private readonly string _Description;
        public string Description => _Description;

        private readonly ImmutableArray<JsonRpcParameter> _Parameters;
        public ImmutableArray<JsonRpcParameter> Parameters => _Parameters;

        private readonly MethodInfo _MethodInfo;
        [JsonIgnore]
        public MethodInfo MethodInfo => _MethodInfo;

        private readonly JsonRpcClass _ParentClass;
        [JsonIgnore]
        public JsonRpcClass ParentClass => _ParentClass;

        private readonly Type _ParamsType;
        [JsonIgnore]
        public Type ParamsType => _ParamsType;

        private readonly string _FullName;
        [JsonIgnore]
        public string FullName => _FullName;

        public JsonRpcMethod(JsonRpcClass parent, MethodInfo info)
        {
            _ParentClass = parent;
            _MethodInfo = info;

            var attribute = _MethodInfo.GetCustomAttribute<JsonRpcMethodAttribute>();

            _Name = attribute.Name ?? _MethodInfo.Name.ToLowerFirstChar();
            var asyncIndex = _Name.LastIndexOf("Async", StringComparison.Ordinal);
            if (asyncIndex > -1)
            {
                _Name = _Name.Remove(asyncIndex);
            }
            _FullName = $"{_ParentClass.Name}.{_Name}";

            _Description = attribute.Description;

            var paramaterInfos = _MethodInfo.GetParameters();
            _ParamsType = paramaterInfos.Where(i => i.IsDefined(typeof(JsonRpcParamsAttribute))).FirstOrDefault()?.ParameterType;

            if (_ParamsType is null)
            {
                _Parameters = paramaterInfos.Select(p =>
                {
                    var attrib = p.GetCustomAttribute<JsonRpcParameterAttribute>();
                    return new JsonRpcParameter(attrib?.Name ?? p.Name, attrib?.Description ?? "", p.ParameterType, p.IsOptional);
                }).ToImmutableArray();
            }
            else
            {
                if (paramaterInfos.Length > 1)
                {
                    throw new InvalidOperationException($"The {nameof(JsonRpcParamsAttribute)} attribute is not valid on the method '{_ParentClass.Name}.{_MethodInfo.Name}' because it has multiple parameters. {nameof(JsonRpcParamsAttribute)} must be applied to the only parameter.");
                }

                _Parameters = _ParamsType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanWrite)
                    .Select(p =>
                    {
                        var attrib = p.GetCustomAttribute<JsonRpcParameterAttribute>();
                        return new JsonRpcParameter(attrib?.Name ?? p.Name, attrib?.Description ?? "", p.PropertyType, !p.IsDefined(typeof(RequiredAttribute)) && !p.IsDefined(typeof(JsonRequiredAttribute)));
                    }).ToImmutableArray();
            }
        }
    }
}