#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNullability
{
    public class Contract
    {
        [JsonRpcParameter(Required = true, Nullable = false)]
        public string RequiredValue { get; set; } = "";
        [JsonRpcParameter(Required = true, Nullable = true)]
        public string? RequiredNull { get; set; }
        [JsonRpcParameter(Required = false, Nullable = false)]
        public string? OptionalValue { get; set; }
        [JsonRpcParameter(Required = false, Nullable = true)]
        public string OptionalNull { get; set; } = "";
        public int? Age { get; set; }
        public int Count { get; set; }
        public string Name { get; set; } = "";
        public string? Alias { get; set; }
        public List<string> Names { get; set; } = new List<string>();
        public List<string?> Aliases { get; set; } = new List<string?>();
        public List<string>? OptionalNames { get; set; }
        public Dictionary<string, string?> Labels { get; set; } = new Dictionary<string, string?>();
        public string?[]? Entries { get; set; }
        public Box<string> StrictBox { get; set; } = new Box<string>();
        public Box<string?> NullableBox { get; set; } = new Box<string?>();
        public Contract? Next { get; set; }
        public object Anything { get; set; } = new object();
        public Tone? Tone { get; set; }
        [JsonRequired]
        public string? PresenceOnly { get; set; }
        [JsonRequired, JsonRpcParameter(Required = false)]
        public string? OverridePresence { get; set; }
        [JsonRpcParameter(Nullable = false)]
        public Box<string?>? StrictReference { get; set; }
        [JsonRpcParameter(Nullable = false)]
        public int? StrictNumber { get; set; }
    }
}
