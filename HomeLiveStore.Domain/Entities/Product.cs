using System.ComponentModel.DataAnnotations;

namespace HomeLiveStore.Domain.Entities
{
  public class Product : BaseEntity
  {
    public int ProductCategoryId { get; set; }
    public ProductCategory ProductCategory { get; set; }

    [StringLength(250)]
    public string Definition { get; set; } = string.Empty;

    [StringLength(1500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(30)]
    public string Barcode { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public int StockAmount { get; set; }

    [StringLength(400)]
    public string CoverPhoto { get; set; } = string.Empty;

    public int VatRate { get; set; }
    public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public decimal UnitPrice => Math.Round(Price + (Price * VatRate / 100m), 2, MidpointRounding.AwayFromZero);
  }
}
