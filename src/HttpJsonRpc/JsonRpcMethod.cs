using System;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Runtime.CompilerServices;

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
            if (attribute.Name == null && _Name.EndsWith("Async", StringComparison.Ordinal))
            {
                _Name = _Name.Substring(0, _Name.Length - 5);
            }
            _FullName = $"{_ParentClass.Name}.{_Name}";

            _Description = attribute.Description;

            var paramaterInfos = _MethodInfo.GetParameters();
            if (info.ContainsGenericParameters || info.ReturnType.IsByRef || info.ReturnType.IsPointer ||
                paramaterInfos.Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer) ||
                (info.ReturnType == typeof(void) && info.IsDefined(typeof(AsyncStateMachineAttribute))))
                throw new InvalidOperationException($"Unsupported RPC signature: {info.DeclaringType.FullName}.{info.Name}");
            _ParamsType = paramaterInfos.Where(i => i.IsDefined(typeof(JsonRpcParamsAttribute))).FirstOrDefault()?.ParameterType;

            if (_ParamsType is null)
            {
                _Parameters = paramaterInfos.Where(p => p.ParameterType != typeof(CancellationToken) && p.GetCustomAttribute<JsonRpcParameterAttribute>()?.Ignore != true).Select(p =>
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
                    .Where(p => p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0 &&
                        p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always &&
                        p.GetCustomAttribute<JsonRpcParameterAttribute>()?.Ignore != true)
                    .Select(p =>
                    {
                        var attrib = p.GetCustomAttribute<JsonRpcParameterAttribute>();
                        var name = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                            JsonRpc.SerializerOptions.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name;
                        return new JsonRpcParameter(name, attrib?.Description ?? "", p.PropertyType, !p.IsRequired());
                    }).ToImmutableArray();
            }
        }
    }
}
