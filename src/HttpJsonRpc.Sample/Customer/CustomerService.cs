using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HttpJsonRpc.Sample
{
    public class CustomerService
    {
        private Customer[] Customers { get; } = new[]
        {
            new Customer { Id = 1, FirstName = "Emma", LastName = "Wilson", Phone = "(555) 321-9876", Email = "emma.wilson@example.com" },
            new Customer { Id = 2, FirstName = "Liam", LastName = "Martinez", Phone = "(555) 487-2341", Email = "liam.martinez@example.com" },
            new Customer { Id = 3, FirstName = "Sophia", LastName = "Thompson", Phone = "(555) 692-1578", Email = "sophia.thompson@example.com" },
            new Customer { Id = 4, FirstName = "Noah", LastName = "Garcia", Phone = "(555) 813-4602", Email = "noah.garcia@example.com" },
            new Customer { Id = 5, FirstName = "Olivia", LastName = "Anderson", Phone = "(555) 279-3456", Email = "olivia.anderson@example.com" },
            new Customer { Id = 6, FirstName = "Mason", LastName = "Clark", Phone = "(555) 156-7893", Email = "mason.clark@example.com" },
            new Customer { Id = 7, FirstName = "Ava", LastName = "Rodriguez", Phone = "(555) 934-5127", Email = "ava.rodriguez@example.com" },
            new Customer { Id = 8, FirstName = "Ethan", LastName = "Lewis", Phone = "(555) 678-2349", Email = "ethan.lewis@example.com" },
            new Customer { Id = 9, FirstName = "Isabella", LastName = "Lee", Phone = "(555) 401-8765", Email = "isabella.lee@example.com" },
            new Customer { Id = 10, FirstName = "James", LastName = "Harris", Phone = "(555) 523-1904", Email = "james.harris@example.com" }
        };

        private async Task<IEnumerable<Customer>> CreateQueryAsync(CustomerFilter filter)
        {
            await Task.CompletedTask;

            var query = Customers.AsEnumerable();

            if (filter.Id is not null)
            {
                query = query.Where(i => i.Id == filter.Id.Value);
            }

            if (filter.FirstName is not null)
            {
                query = query.Where(i => i.FirstName == filter.FirstName);
            }

            if (filter.LastName is not null)
            {
                query = query.Where(i => i.LastName == filter.LastName);
            }

            if (filter.Phone is not null)
            {
                query = query.Where(i => i.Phone == filter.Phone);
            }

            if (filter.Email is not null)
            {
                query = query.Where(i => i.Email == filter.Email);
            }

            return query;
        }

        public async Task<Customer[]> ListAsync(CustomerFilter filter)
        {
            return (await CreateQueryAsync(filter)).ToArray();
        }

        public async Task<Customer> GetAsync(CustomerFilter filter)
        {
            return (await CreateQueryAsync(filter)).Single();
        }
    }
}
