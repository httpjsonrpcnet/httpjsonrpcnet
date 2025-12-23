using System;
using System.Threading.Tasks;

namespace HttpJsonRpc
{
    public class OpenRpcTaskTypeConverter : IOpenRpcTypeConverter
    {
        public bool CanConvert(OpenRpcSchemaGenerator generator, Type type)
        {
            if (type == typeof(Task)) return true;
            if (type == typeof(ValueTask)) return true;
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(Task<>)) return true;
                if (genericType == typeof(ValueTask<>)) return true;
            }

            return false;
        }

        public OpenRpcTypeInfo Convert(OpenRpcSchemaGenerator generator, OpenRpcTypeInfo info)
        {
            var unwrappedType = UnwrapTaskType(info.Type);
            return generator.GetTypeInfo(info.With(i =>
            {
                i.Type = unwrappedType;
            }));
        }

        public string GetName(OpenRpcSchemaGenerator generator, Type type)
        {
            var unwrappedType = UnwrapTaskType(type);
            return generator.GetName(unwrappedType);
        }

        private Type UnwrapTaskType(Type type)
        {
            if (type == typeof(Task)) return typeof(void);
            if (type == typeof(ValueTask)) return typeof(void);
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(Task<>)) return type.GetGenericArguments()[0];
                if (genericType == typeof(ValueTask<>)) return type.GetGenericArguments()[0];
            }

            throw new InvalidOperationException($"Unsupported Task type '{type}'");
        }
    }
}
