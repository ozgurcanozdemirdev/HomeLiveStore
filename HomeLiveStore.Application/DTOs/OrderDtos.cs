using System.ComponentModel.DataAnnotations;
using HomeLiveStore.Domain.Enums;

namespace HomeLiveStore.Application.DTOs
{
  /// <summary>Siparis satiri olusturma istegi.</summary>
  public sealed record BasketLineRequest
  {
    public const int MaximumQuantity = 999;

    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    [Range(1, MaximumQuantity)]
    public int Quantity { get; init; }
  }

  /// <summary>Siparis olusturma istegi.</summary>
  public sealed record OrderCreateRequest : IValidatableObject
  {
    [Required, MaxLength(400)]
    public string DeliveryAddress { get; init; } = string.Empty;

    [Required]
    public Guid RequestId { get; init; }

    [Required, MinLength(1)]
    public List<BasketLineRequest> OrderLines { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      // Listenin dolu olması yeterli değildir; boş elemanlar servise ulaşmadan reddedilir.
      if (OrderLines is not null && OrderLines.Any(line => line is null))
      {
        yield return new ValidationResult("Sipariş satırı boş olamaz.", new[] { nameof(OrderLines) });
      }
    }
  }

  /// <summary>Siparis durum guncelleme istegi.</summary>
  public sealed record UpdateOrderStatusRequest
  {
    [Required]
    public OrderStatus Status { get; init; }
  }

  /// <summary>Siparis cevabi.</summary>
  public sealed record OrderResponse(
    int Id,
    DateTimeOffset OrderDate,
    decimal TotalAmount,
    string DeliveryAddress,
    OrderStatus Status,
    string CustomerName,
    string CustomerEmail,
    IReadOnlyList<OrderLineResponse> OrderLines);

  /// <summary>Siparis satiri cevabi.</summary>
  public sealed record OrderLineResponse(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotalAmount);
}
