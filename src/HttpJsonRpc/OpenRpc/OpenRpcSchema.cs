using System.Collections.Generic;
using System.Linq;
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
        public string Description { get; set; }
        public string Format { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string Pattern { get; set; }
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public double? ExclusiveMinimum { get; set; }
        public double? ExclusiveMaximum { get; set; }
        public double? MultipleOf { get; set; }
        public int? MinItems { get; set; }
        public int? MaxItems { get; set; }
        public bool? UniqueItems { get; set; }
        public object[] Enum { get; set; }
        public string[] Required { get; set; }
        public OpenRpcSchema AdditionalProperties { get; set; }
        public OpenRpcSchema[] AllOf { get; set; }
        public OpenRpcSchema[] AnyOf { get; set; }
        public OpenRpcSchema Not { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Nullable { get; set; }

        [JsonPropertyName("$ref")]
        public string Ref { get; set; }

        internal OpenRpcSchema Copy()
        {
            var copy = new OpenRpcSchema
            {
                Items = Items?.Copy(), Description = Description, Format = Format, Ref = Ref, Nullable = Nullable,
                Required = Required?.ToArray(), Enum = Enum?.ToArray(),
                MinLength = MinLength, MaxLength = MaxLength, Pattern = Pattern,
                Minimum = Minimum, Maximum = Maximum, ExclusiveMinimum = ExclusiveMinimum,
                ExclusiveMaximum = ExclusiveMaximum, MultipleOf = MultipleOf,
                MinItems = MinItems, MaxItems = MaxItems, UniqueItems = UniqueItems,
                Properties = Properties?.ToDictionary(p => p.Key, p => p.Value.Copy()),
                AdditionalProperties = AdditionalProperties?.Copy(),
                AllOf = AllOf?.Select(s => s.Copy()).ToArray(),
                AnyOf = AnyOf?.Select(s => s.Copy()).ToArray(), Not = Not?.Copy()
            };
            foreach (var type in _Types) copy.AddType(type);
            return copy;
        }

        internal void RemoveType(string type) => _Types.Remove(type);

        public void AddType(string type)
        {
            if (!_Types.Contains(type))
            {
                _Types.Add(type);
            }
        }
    }
}
