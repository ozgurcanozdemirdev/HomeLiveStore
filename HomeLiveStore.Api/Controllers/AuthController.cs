using System.Security.Claims;
using HomeLiveStore.Application.DTOs;
using HomeLiveStore.Application.Exceptions;
using HomeLiveStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Api.Controllers
{
  [ApiController]
  [Route("api/auth")]
  public sealed class AuthController(IAuthService authService, IAntiforgery antiforgery) : ControllerBase
  {
    [HttpGet("csrf")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public ActionResult<object> Csrf()
    {
      var tokens = antiforgery.GetAndStoreTokens(HttpContext);
      return Ok(new { token = tokens.RequestToken });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
      var user = await authService.RegisterAsync(request, cancellationToken);
      await SignInAsync(user);
      antiforgery.GetAndStoreTokens(HttpContext);
      return Created("/api/auth/me", user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
      var user = await authService.ValidateLoginAsync(request, cancellationToken);
      await SignInAsync(user);
      antiforgery.GetAndStoreTokens(HttpContext);
      return Ok(user);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
      await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
      antiforgery.GetAndStoreTokens(HttpContext);
      return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
      return Ok(await authService.GetUserAsync(CurrentUserId(), cancellationToken));
    }

    private Task SignInAsync(UserResponse user)
    {
      return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, StoreApiExtensions.CreatePrincipal(user));
    }

    private int CurrentUserId()
    {
      var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
      return int.TryParse(value, out var id) ? id : throw new StoreValidationException("Oturum bulunamadi.");
    }
  }
}
