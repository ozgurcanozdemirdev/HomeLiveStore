using System.ComponentModel.DataAnnotations;

namespace HomeLiveStore.Application.DTOs
{
  /// <summary>Kategori cevabi.</summary>
  public sealed record CategoryListDto(int Id, string Definition, string Description, bool IsActive);

  /// <summary>Kategori kaydetme istegi.</summary>
  public sealed record CategorySaveDto
  {
    [Required, MaxLength(150)]
    public string Definition { get; init; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
  }
}
