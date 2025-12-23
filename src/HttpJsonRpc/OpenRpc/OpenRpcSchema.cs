using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    public class OpenRpcSchema
    {
        private List<string> _Types = new List<string>();
        public object Type
        {
            get
            {
                switch (_Types.Count)
                {
                    case 0:
                        return null;
                    case 1:
                        return _Types[0];
                    default:
                        return _Types.ToArray();
                }
            }
        }

        public OpenRpcSchema Items { get; set; }
        public Dictionary<string, OpenRpcSchema> Properties { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Nullable { get; set; }

        [JsonPropertyName("$ref")]
        public string Ref { get; set; }

        public void AddType(string type)
        {
            if (!_Types.Contains(type))
            {
                _Types.Add(type);
            }
        }
    }
}