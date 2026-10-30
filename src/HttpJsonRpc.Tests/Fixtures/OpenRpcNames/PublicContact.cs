namespace HttpJsonRpc.Tests.Fixtures.OpenRpcNames
{
    [OpenRpcSchema(Name = "Contact", Description = "A public contact.")]
    public class PublicContact
    {
        public PublicContact Parent { get; set; }
    }
}
