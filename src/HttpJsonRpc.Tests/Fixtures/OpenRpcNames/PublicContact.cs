namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNames
{
    [JsonRpcSchema(Name = "Contact", Description = "A public contact.")]
    public class PublicContact
    {
        public PublicContact Parent { get; set; }
    }
}
