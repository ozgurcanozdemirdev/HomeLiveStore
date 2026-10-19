using System.ComponentModel.DataAnnotations;

namespace HomeLiveStore.Domain.Entities
{
  public class ProductCategory : BaseEntity
  {
    [StringLength(150)]
    public string Definition { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = new List<Product>();
  }
}
