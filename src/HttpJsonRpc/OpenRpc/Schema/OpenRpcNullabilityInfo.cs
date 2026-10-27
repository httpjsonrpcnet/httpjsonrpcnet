using System;
using System.Linq;

namespace HttpJsonRpc
{
    internal sealed class OpenRpcNullabilityInfo
    {
        internal Type Type { get; }
        internal bool? Nullable { get; }
        internal OpenRpcNullabilityInfo[] Arguments { get; }
        internal OpenRpcNullabilityInfo(Type type, bool? nullable, params OpenRpcNullabilityInfo[] arguments)
        { Type = type; Nullable = nullable; Arguments = arguments; }

        internal OpenRpcNullabilityInfo WithNullable(bool? nullable) => new OpenRpcNullabilityInfo(Type, nullable, Arguments);
        // Root nullability belongs to the reference, not the shared component body.
        internal string ComponentSuffix => Arguments.Any(a => a.HasMetadata) ? "_N" + string.Join("_", Arguments.Select(a => a.Identity)) : "";
        private bool HasMetadata => (!Type.IsValueType && Nullable.HasValue) || Arguments.Any(a => a.HasMetadata);
        private string Identity => (Nullable == true ? "2" : Nullable == false ? "1" : "0") + "b" + string.Join("_", Arguments.Select(a => a.Identity)) + "e";
    }
}
