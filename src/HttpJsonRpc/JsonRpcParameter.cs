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

        public JsonRpcParameter(string name, string description, string type, bool optional)
        {
            _Name = name;
            _Description = description;
            _Type = type;
            _Optional = optional;
        }
    }
}