using System;
using System.Collections.Generic;
using System.Reflection;

namespace HttpJsonRpc
{
    public class OpenRpcOptions
    {
        // Configure before starting the server. Callbacks run in order and must be thread-safe.
        public List<Action<OpenRpcSchemaTransformerContext>> SchemaTransformers { get; } = new List<Action<OpenRpcSchemaTransformerContext>>();
        public List<Action<OpenRpcSchemaUsageTransformerContext>> SchemaUsageTransformers { get; } = new List<Action<OpenRpcSchemaUsageTransformerContext>>();
        public List<Action<OpenRpcMethodTransformerContext>> MethodTransformers { get; } = new List<Action<OpenRpcMethodTransformerContext>>();
        public List<Action<OpenRpcDocumentTransformerContext>> DocumentTransformers { get; } = new List<Action<OpenRpcDocumentTransformerContext>>();

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
