using System.ComponentModel.DataAnnotations;

namespace HomeLiveStore.Application.DTOs
{
  /// <summary>Yeni musteri kaydi istegi.</summary>
  public sealed record RegisterRequest
  {
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string Surname { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; init; } = string.Empty;
  }

  /// <summary>Oturum acma istegi.</summary>
  public sealed record LoginRequest
  {
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string Password { get; init; } = string.Empty;
  }

  /// <summary>Kimlik dogrulanan kullanici bilgisi.</summary>
  public sealed record UserResponse(int Id, string Name, string Surname, string Email, string Role);

  /// <summary>Admin kullanici listesi satiri.</summary>
  public sealed record UserListDto(int Id, string Name, string Surname, string Email, string Role);
}
