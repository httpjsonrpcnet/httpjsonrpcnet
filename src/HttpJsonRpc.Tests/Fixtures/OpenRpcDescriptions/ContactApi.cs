using System.Threading.Tasks;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    [JsonRpcClass("describedContact")]
    public class ContactApi
    {
        [JsonRpcMethod, OpenRpcMethod(Description = "Creates a contact.")]
        [OpenRpcResult(Description = "The newly created contact.")]
        public Task<Contact> Create([OpenRpcParameter(Description = "Input values.")] Contact contact) => Task.FromResult(contact);
        [JsonRpcMethod, OpenRpcResult(Description = "The matching contact.")]
        public Contact Find([JsonRpcParams] ContactRequest request) => null;
        [JsonRpcMethod, OpenRpcResult(Description = "No value.")]
        public Task Complete() => Task.CompletedTask;
    }
}
