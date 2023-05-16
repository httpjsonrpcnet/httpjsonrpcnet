using System.Reflection;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class JsonRpcParameter
    {
        private readonly string _Name;
        public string Name => _Name;

        private readonly string _Description;
        public string Description => _Description;

        private readonly string _Type;
        public string Type => _Type;

        private readonly bool _Optional;
        public bool Optional => _Optional;

        private readonly ParameterInfo _ParameterInfo;
        [JsonIgnore]
        public ParameterInfo ParameterInfo => _ParameterInfo;

        public JsonRpcParameter(ParameterInfo info)
        {
            _ParameterInfo = info;
            var attribute = _ParameterInfo.GetCustomAttribute<JsonRpcParameterAttribute>();
            _Name = attribute?.Name ?? _ParameterInfo.Name;
            _Description = attribute?.Description;
            _Type = JsonTypeMap.GetJsonType(_ParameterInfo.ParameterType);
            _Optional = _ParameterInfo.IsOptional;
        }
    }
}