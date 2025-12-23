using System;

namespace HttpJsonRpc
{
    public class OpenRpcTypeInfo
    {
        public Type Type { get; set; }
        public bool Nullable { get; set; }
        public bool CanRefence { get; set; } = true;
        public bool IsOpaque { get; set; }

        public OpenRpcTypeInfo With(Action<OpenRpcTypeInfo> update)
        {
            var copy = (OpenRpcTypeInfo)MemberwiseClone();
            update(copy);
            return copy;
        }
    }
}
