using System.Threading.Tasks;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcTransformers
{
    [JsonRpcClass("contact")]
    public class ContactApi
    {
        [JsonRpcMethod] public Task<Contact> Create(Contact contact) => Task.FromResult(contact);
        [JsonRpcMethod] public Contact Echo(Contact contact) => contact;
        [JsonRpcMethod] public Contact Search([JsonRpcParams] SearchRequest request) => null;
    }
}
