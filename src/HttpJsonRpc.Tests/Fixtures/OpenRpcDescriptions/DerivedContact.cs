namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    public class DerivedContact : Contact
    {
        [OpenRpcProperty(Description = "Overridden shipping location.")]
        public new Address Shipping { get; set; }
    }
}
