#nullable enable
using System.ComponentModel.DataAnnotations;

namespace HomeLiveStore.Application.DTOs
{
  /// <summary>Urun listeleme cevabi.</summary>
  public sealed record ProductListDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string Definition,
    string Description,
    string Barcode,
    decimal Price,
    int VatRate,
    decimal UnitPrice,
    int StockAmount,
    string CoverPhoto,
    bool IsActive)
  {
    public IReadOnlyList<string> ImagePaths { get; init; } = Array.Empty<string>();
  }

  /// <summary>Sayfali urun cevabi.</summary>
  public sealed record ProductPageResponse(IReadOnlyList<ProductListDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);

  /// <summary>Urun kaydetme istegi.</summary>
  public sealed record ProductSaveDto
  {
    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    [Required, MaxLength(250)]
    public string Definition { get; init; } = string.Empty;

    [MaxLength(1500)]
    public string Description { get; init; } = string.Empty;

    [Required, MaxLength(30)]
    public string Barcode { get; init; } = string.Empty;

    // Sınırlar kodda noktayla yazılır; Türkçe sistemlerde de aynı tutarlar doğrulanır.
    [Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Range(0, 100)]
    public int VatRate { get; init; }

    [Range(0, int.MaxValue)]
    public int StockAmount { get; init; }

    // Düzenleme formunun açıldığı stok; yeni ürün oluştururken gönderilmez.
    [Range(0, int.MaxValue)]
    public int? OriginalStockAmount { get; init; }

    [MaxLength(400)]
    public string CoverPhoto { get; init; } = string.Empty;

    // Null gönderilirse mevcut galeri korunur; boş liste bütün ek görselleri kaldırır.
    public IReadOnlyList<string>? ImagePaths { get; init; }

    public bool IsActive { get; init; } = true;
  }
}
