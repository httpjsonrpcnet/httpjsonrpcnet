using System;

namespace HttpJsonRpc
{
    /// <summary>Describes the success result of an RPC method.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class JsonRpcResultAttribute : Attribute
    {
        public string Description { get; set; }
        internal OpenRpcSchemaConstraints Constraints { get; } = new OpenRpcSchemaConstraints();
        /// <summary>Describes the JSON Schema format constraint in discovery only.</summary>
        public string Format { get => Constraints.Format; set => Constraints.Format = value; }
        /// <summary>Describes the JSON Schema minLength constraint in discovery only.</summary>
        public int MinLength { get => Constraints.MinLength.GetValueOrDefault(); set => Constraints.MinLength = value; }
        /// <summary>Describes the JSON Schema maxLength constraint in discovery only.</summary>
        public int MaxLength { get => Constraints.MaxLength.GetValueOrDefault(); set => Constraints.MaxLength = value; }
        /// <summary>Describes the JSON Schema pattern constraint in discovery only.</summary>
        public string Pattern { get => Constraints.Pattern; set => Constraints.Pattern = value; }
        /// <summary>Describes the JSON Schema minimum constraint in discovery only.</summary>
        public double Minimum { get => Constraints.Minimum.GetValueOrDefault(); set => Constraints.Minimum = value; }
        /// <summary>Describes the JSON Schema maximum constraint in discovery only.</summary>
        public double Maximum { get => Constraints.Maximum.GetValueOrDefault(); set => Constraints.Maximum = value; }
        /// <summary>Describes the JSON Schema exclusiveMinimum constraint in discovery only.</summary>
        public double ExclusiveMinimum { get => Constraints.ExclusiveMinimum.GetValueOrDefault(); set => Constraints.ExclusiveMinimum = value; }
        /// <summary>Describes the JSON Schema exclusiveMaximum constraint in discovery only.</summary>
        public double ExclusiveMaximum { get => Constraints.ExclusiveMaximum.GetValueOrDefault(); set => Constraints.ExclusiveMaximum = value; }
        /// <summary>Describes the JSON Schema multipleOf constraint in discovery only.</summary>
        public double MultipleOf { get => Constraints.MultipleOf.GetValueOrDefault(); set => Constraints.MultipleOf = value; }
        /// <summary>Describes the JSON Schema minItems constraint in discovery only.</summary>
        public int MinItems { get => Constraints.MinItems.GetValueOrDefault(); set => Constraints.MinItems = value; }
        /// <summary>Describes the JSON Schema maxItems constraint in discovery only.</summary>
        public int MaxItems { get => Constraints.MaxItems.GetValueOrDefault(); set => Constraints.MaxItems = value; }
        /// <summary>Describes the JSON Schema uniqueItems constraint in discovery only.</summary>
        public bool UniqueItems { get => Constraints.UniqueItems.GetValueOrDefault(); set => Constraints.UniqueItems = value; }

        /// <summary>Overrides result nullability in discovery only. Omit to infer from CLR metadata.</summary>
        public bool Nullable { get => NullableOverride.GetValueOrDefault(); set => NullableOverride = value; }
        internal bool? NullableOverride { get; private set; }
    }
}
