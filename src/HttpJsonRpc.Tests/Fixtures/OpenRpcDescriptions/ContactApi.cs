using System.Threading.Tasks;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    [JsonRpcClass("describedContact")]
    public class ContactApi
    {
        [JsonRpcMethod(Description = "Creates a contact.")]
        [JsonRpcResult(Description = "The newly created contact.")]
        public Task<Contact> Create([JsonRpcParameter(Description = "Input values.")] Contact contact) => Task.FromResult(contact);
        [JsonRpcMethod, JsonRpcResult(Description = "The matching contact.")]
        public Contact Find([JsonRpcParams] ContactRequest request) => null;
        [JsonRpcMethod, JsonRpcResult(Description = "No value.")]
        public Task Complete() => Task.CompletedTask;
    }
}
