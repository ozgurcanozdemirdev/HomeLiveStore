using HomeLiveStore.Application.DTOs;

namespace HomeLiveStore.Application.Interfaces.Services
{
  public interface IAuthService
  {
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> ValidateLoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> GetUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserListDto>> GetUsersAsync(CancellationToken cancellationToken = default);
  }
}
