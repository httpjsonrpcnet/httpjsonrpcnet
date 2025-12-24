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
        public async Task<Customer[]> ListAsync(ListParams<CustomerFilter> @params)
        {
            return await CustomerService.ListAsync(@params.Filter);
        }

        [JsonRpcMethod]
        public async Task<Customer> GetAsync(ListParams<CustomerFilter> @params)
        {
            return await CustomerService.GetAsync(@params.Filter);
        }
    }
}
