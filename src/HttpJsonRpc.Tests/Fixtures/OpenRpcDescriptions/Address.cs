namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    [JsonRpcSchema(Description = "A postal address.")]
    public class Address
    {
        [JsonRpcParameter(Description = "City or town.")]
        public string City { get; set; }
    }
}
