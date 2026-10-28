using System.ComponentModel.DataAnnotations;
namespace HttpJsonRpc.Tests.Fixtures.OpenRpcConstraints
{
    public class ConstraintRequest
    {
        [JsonRpcParameter(Format = "email", MinLength = 3, MaxLength = 30, Pattern = "^[a-z]+@example[.]com$")]
        public string Email { get; set; }
        [JsonRpcParameter(Minimum = 0, Maximum = 10, ExclusiveMinimum = 0, ExclusiveMaximum = 10, MultipleOf = 0.5)]
        public double Amount { get; set; }
        [JsonRpcParameter(MinItems = 1, MaxItems = 2, UniqueItems = true)]
        public string[] Tags { get; set; }
        [JsonRpcParameter(MinItems = 0, MaxItems = 0, UniqueItems = false)]
        public string[] Empty { get; set; }
        public string[] Unrestricted { get; set; }
        [JsonRpcParameter(MinLength = 0, MaxLength = 0)]
        public string EmptyText { get; set; }
        [JsonRpcParameter(Minimum = 0, Maximum = 0, Nullable = true)]
        public double? Zero { get; set; }
        [StringLength(1), RegularExpression("x")]
        public string ExternalAnnotations { get; set; }
    }
}
