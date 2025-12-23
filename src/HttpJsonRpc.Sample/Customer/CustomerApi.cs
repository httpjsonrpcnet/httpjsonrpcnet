using System.Threading.Tasks;

namespace HttpJsonRpc.Sample
{
    [JsonRpcClass("customer")]
    public class CustomerApi
    {
        private CustomerService CustomerService { get; }

        public CustomerApi(CustomerService customerService)
        {
            CustomerService = customerService;
        }

        [JsonRpcMethod]
        public async Task<Customer[]> ListAsync([JsonRpcParams] ListCustomerParams @params)
        {
            return await CustomerService.ListAsync(@params.Filter);
        }

        [JsonRpcMethod]
        public async Task<Customer> GetAsync([JsonRpcParams] ListCustomerParams @params)
        {
            return await CustomerService.GetAsync(@params.Filter);
        }
    }
}
