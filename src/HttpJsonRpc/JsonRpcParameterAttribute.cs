using System;

namespace HttpJsonRpc
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public class JsonRpcParameterAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Ignore { get; set; }
        /// <summary>Overrides presence in discovery only. Omit to infer from the existing contract.</summary>
        public bool Required { get => RequiredOverride.GetValueOrDefault(); set => RequiredOverride = value; }
        /// <summary>Overrides value nullability in discovery only. Omit to infer from CLR metadata.</summary>
        public bool Nullable { get => NullableOverride.GetValueOrDefault(); set => NullableOverride = value; }
        internal bool? RequiredOverride { get; private set; }
        internal bool? NullableOverride { get; private set; }
    }
}