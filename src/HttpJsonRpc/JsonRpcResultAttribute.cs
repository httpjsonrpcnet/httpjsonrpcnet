using System;

namespace HttpJsonRpc
{
    /// <summary>Describes the success result of an RPC method.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class JsonRpcResultAttribute : Attribute
    {
        public string Description { get; set; }
        /// <summary>Overrides result nullability in discovery only. Omit to infer from CLR metadata.</summary>
        public bool Nullable { get => NullableOverride.GetValueOrDefault(); set => NullableOverride = value; }
        internal bool? NullableOverride { get; private set; }
    }
}
