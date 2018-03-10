using System.Collections.ObjectModel;

namespace HttpJsonRpc
{
    public class JsonRpcParameterCollection : KeyedCollection<string, JsonRpcParameter>
    {
        protected override string GetKeyForItem(JsonRpcParameter item)
        {
            return item.Name;
        }
    }
}