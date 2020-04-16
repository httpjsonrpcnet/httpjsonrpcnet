using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace HttpJsonRpc
{
    public class JsonRpcMethod
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, JsonRpcParameter> Parameters { get; } = new Dictionary<string, JsonRpcParameter>();

        [JsonIgnore]
        public MethodInfo MethodInfo { get; set; }

        [JsonIgnore]
        public JsonRpcClass ParentClass { get; set; }
    }
}