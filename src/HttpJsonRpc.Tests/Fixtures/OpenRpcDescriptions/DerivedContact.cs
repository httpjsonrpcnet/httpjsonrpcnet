namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    public class DerivedContact : Contact
    {
        [JsonRpcParameter(Description = "Overridden shipping location.")]
        public new Address Shipping { get; set; }
    }
}
