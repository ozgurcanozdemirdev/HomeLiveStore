using HomeLiveStore.Application.DTOs;
using HomeLiveStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Api.Controllers
{
  [ApiController]
  [Route("api/admin")]
  [Authorize(Roles = "Manager")]
  public sealed class AdminApiController(IAuthService authService, IDashboardService dashboardService) : ControllerBase
  {
    [HttpGet("users")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<IReadOnlyList<UserListDto>>> Users(CancellationToken cancellationToken)
    {
      return Ok(await authService.GetUsersAsync(cancellationToken));
    }

    [HttpGet("dashboard")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken cancellationToken)
    {
      return Ok(await dashboardService.GetDashboardAsync(cancellationToken));
    }
  }
}
