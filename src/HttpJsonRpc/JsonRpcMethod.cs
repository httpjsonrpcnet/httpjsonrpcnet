using System.Reflection;
using Newtonsoft.Json;

namespace HttpJsonRpc
{
    public class JsonRpcMethod
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JsonRpcParameterCollection Parameters { get; } = new JsonRpcParameterCollection();

        [JsonIgnore]
        public MethodInfo MethodInfo { get; set; }
    }
}