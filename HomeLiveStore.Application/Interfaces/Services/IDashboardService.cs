using HomeLiveStore.Application.DTOs;

namespace HomeLiveStore.Application.Interfaces.Services
{
  public interface IDashboardService
  {
    Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default);
  }
}
