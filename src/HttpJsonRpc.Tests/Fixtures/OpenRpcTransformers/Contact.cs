using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcTransformers
{
    public class Contact
    {
        public string Name { get; set; }
        [JsonPropertyName("birthday")] public string BirthDate { get; set; }
        public string[] Labels { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
    }
}
