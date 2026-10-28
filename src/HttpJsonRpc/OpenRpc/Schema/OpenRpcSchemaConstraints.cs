using System;

namespace HttpJsonRpc
{
    internal sealed class OpenRpcSchemaConstraints
    {
        internal string Format { get; set; }
        internal int? MinLength { get; set; }
        internal int? MaxLength { get; set; }
        internal string Pattern { get; set; }
        internal double? Minimum { get; set; }
        internal double? Maximum { get; set; }
        internal double? ExclusiveMinimum { get; set; }
        internal double? ExclusiveMaximum { get; set; }
        internal double? MultipleOf { get; set; }
        internal int? MinItems { get; set; }
        internal int? MaxItems { get; set; }
        internal bool? UniqueItems { get; set; }

        internal bool HasValues => Format != null ||
            MinLength != null ||
            MaxLength != null ||
            Pattern != null ||
            Minimum != null ||
            Maximum != null ||
            ExclusiveMinimum != null ||
            ExclusiveMaximum != null ||
            MultipleOf != null ||
            MinItems != null ||
            MaxItems != null ||
            UniqueItems != null;

        internal void Apply(OpenRpcSchema schema)
        {
            if (MinLength < 0) throw new InvalidOperationException("MinLength must be non-negative.");
            if (MaxLength < 0) throw new InvalidOperationException("MaxLength must be non-negative.");
            if (MinItems < 0) throw new InvalidOperationException("MinItems must be non-negative.");
            if (MaxItems < 0) throw new InvalidOperationException("MaxItems must be non-negative.");
            if (Minimum.HasValue && (double.IsNaN(Minimum.Value) || double.IsInfinity(Minimum.Value)))
                throw new InvalidOperationException("Minimum must be finite.");
            if (Maximum.HasValue && (double.IsNaN(Maximum.Value) || double.IsInfinity(Maximum.Value)))
                throw new InvalidOperationException("Maximum must be finite.");
            if (ExclusiveMinimum.HasValue && (double.IsNaN(ExclusiveMinimum.Value) || double.IsInfinity(ExclusiveMinimum.Value)))
                throw new InvalidOperationException("ExclusiveMinimum must be finite.");
            if (ExclusiveMaximum.HasValue && (double.IsNaN(ExclusiveMaximum.Value) || double.IsInfinity(ExclusiveMaximum.Value)))
                throw new InvalidOperationException("ExclusiveMaximum must be finite.");
            if (MultipleOf.HasValue && (double.IsNaN(MultipleOf.Value) || double.IsInfinity(MultipleOf.Value)))
                throw new InvalidOperationException("MultipleOf must be finite.");
            if (MultipleOf <= 0) throw new InvalidOperationException("MultipleOf must be positive.");
            if (Format != null) schema.Format = Format;
            if (MinLength != null) schema.MinLength = MinLength;
            if (MaxLength != null) schema.MaxLength = MaxLength;
            if (Pattern != null) schema.Pattern = Pattern;
            if (Minimum != null) schema.Minimum = Minimum;
            if (Maximum != null) schema.Maximum = Maximum;
            if (ExclusiveMinimum != null) schema.ExclusiveMinimum = ExclusiveMinimum;
            if (ExclusiveMaximum != null) schema.ExclusiveMaximum = ExclusiveMaximum;
            if (MultipleOf != null) schema.MultipleOf = MultipleOf;
            if (MinItems != null) schema.MinItems = MinItems;
            if (MaxItems != null) schema.MaxItems = MaxItems;
            if (UniqueItems != null) schema.UniqueItems = UniqueItems;
        }
    }
}
