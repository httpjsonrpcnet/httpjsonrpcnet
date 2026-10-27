#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public class NestedContract
    {
        public Dictionary<string, List<string?[]>?> Values { get; set; } = new Dictionary<string, List<string?[]>?>();
        public Dictionary<string, List<string[]>> Strict { get; set; } = new Dictionary<string, List<string[]>>();
        public Pair<int, string?>? Pair { get; set; }
        [JsonInclude]
        public string? Field;
        [JsonIgnore]
        public string? Hidden { get; set; }
        public string? ReadOnly => null;
    }
}
