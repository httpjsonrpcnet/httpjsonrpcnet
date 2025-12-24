using System.ComponentModel.DataAnnotations;

namespace HttpJsonRpc.Sample
{
    public class ListParams<TFilter>
    {
        [Required]
        public TFilter Filter { get; set; }
    }
}
