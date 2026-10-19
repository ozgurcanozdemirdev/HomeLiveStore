using System.ComponentModel.DataAnnotations;

namespace HomeLiveStore.Domain.Entities
{
  public class ProductImage : BaseEntity
  {
    public int ProductId { get; set; }
    public Product Product { get; set; }

    [StringLength(400)]
    public string Path { get; set; } = string.Empty;
  }
}
