using System.ComponentModel.DataAnnotations;

namespace HttpJsonRpc.Sample
{
    public class ListCustomerParams
    {
        [Required]
        public CustomerFilter Filter { get; set; }
    }
}
