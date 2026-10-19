using System.ComponentModel.DataAnnotations.Schema;

namespace HomeLiveStore.Domain.Entities
{
  public class OrderLine : BaseEntity
  {
    public int OrderId { get; set; }
    public Order Order { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int VatRate { get; set; }
    public int Quantity { get; set; }

    public decimal LineTotalAmout { get; set; }

    [NotMapped]
    public decimal LineTotalAmount
    {
      get => LineTotalAmout;
      set => LineTotalAmout = value;
    }
  }
}
