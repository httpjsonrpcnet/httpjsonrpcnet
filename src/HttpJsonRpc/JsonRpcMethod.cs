using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace HttpJsonRpc
{
    public class JsonRpcMethod
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<JsonRpcParameter> Parameters { get; } = new List<JsonRpcParameter>();

        [JsonIgnore]
        public MethodInfo MethodInfo { get; set; }
    }
}