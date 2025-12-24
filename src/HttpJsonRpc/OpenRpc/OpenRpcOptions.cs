using System;
using System.Collections.Generic;
using System.Reflection;

namespace HttpJsonRpc
{
    public class OpenRpcOptions
    {
        public bool IsEnabled { get; set; } = false;

        public OpenRpcInfo Info { get; } = new OpenRpcInfo
        {
            Title = Assembly.GetEntryAssembly()?.GetName().Name,
            Version = Assembly.GetEntryAssembly()?.GetName().Version.ToString()
        };

        public List<Func<JsonRpcContext, JsonRpcMethod, bool>> MethodFilters { get; } = new List<Func<JsonRpcContext, JsonRpcMethod, bool>>
        {
            (context, method) => true
        };

        public List<IOpenRpcTypeConverter> TypeConverters { get; } = new List<IOpenRpcTypeConverter>
        {
            new OpenRpcTaskTypeConverter(),
            new OpenRpcNullableTypeConverter(),
            new OpenRpcStreamTypeConverter(),
            new OpenRpcDictionaryTypeConverter(),
            new OpenRpcObjectTypeConverter()
        };
    }
}
