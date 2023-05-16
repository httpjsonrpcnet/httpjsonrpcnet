using System.Reflection;

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

        public JsonRpcParameter(ParameterInfo info)
        {
            var attribute = info.GetCustomAttribute<JsonRpcParameterAttribute>();
            _Name = attribute?.Name ?? info.Name;
            _Description = attribute?.Description;
            _Type = JsonTypeMap.GetJsonType(info.ParameterType);
            _Optional = info.IsOptional;
        }
    }
}