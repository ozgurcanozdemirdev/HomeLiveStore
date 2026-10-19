using System.ComponentModel.DataAnnotations;
using HomeLiveStore.Domain.Enums;

namespace HomeLiveStore.Domain.Entities
{
  public class Order : BaseEntity
  {
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;
    public decimal TotalAmount { get; set; }

    [StringLength(400)]
    public string DeliveryAdress { get; set; } = string.Empty;

    [StringLength(64)]
    public string RequestId { get; set; } = string.Empty;

    public OrderStatus OrderStatus { get; set; } = OrderStatus.Preparing;
    public int UserId { get; set; }
    public User User { get; set; }
    public ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

    public void SatirEkle(Product product, int quantity)
    {
      if (quantity <= 0)
      {
        throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
      }

      var existing = OrderLines.FirstOrDefault(line => line.ProductId == product.Id);
      if (existing is not null)
      {
        existing.Quantity += quantity;
        CalculateTotal();
        return;
      }

      var unitPrice = product.UnitPrice;
      OrderLines.Add(new OrderLine
      {
        ProductId = product.Id,
        ProductName = product.Definition,
        UnitPrice = unitPrice,
        VatRate = product.VatRate,
        Quantity = quantity,
        LineTotalAmount = Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero)
      });

      CalculateTotal();
    }

    public void CalculateTotal()
    {
      foreach (var line in OrderLines)
      {
        line.LineTotalAmount = Math.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
      }

      TotalAmount = OrderLines.Sum(t => t.LineTotalAmount);
    }

    public bool CanBeCanceledByCustomer() => OrderStatus == OrderStatus.Preparing;
  }
}
