using System.Collections.ObjectModel;

namespace HttpJsonRpc
{
    public class JsonRpcMethodCollection : KeyedCollection<string, JsonRpcMethod>
    {
        protected override string GetKeyForItem(JsonRpcMethod item)
        {
            return item.Name;
        }
    }
}