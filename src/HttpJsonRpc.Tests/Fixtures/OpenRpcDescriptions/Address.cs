namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    [OpenRpcSchema(Description = "A postal address.")]
    public class Address
    {
        [OpenRpcProperty(Description = "City or town.")]
        public string City { get; set; }
    }
}
