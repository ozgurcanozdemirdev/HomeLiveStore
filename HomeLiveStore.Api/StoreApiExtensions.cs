using System.Security.Claims;
using System.Text.Json.Serialization;
using HomeLiveStore.Application;
using HomeLiveStore.Application.Exceptions;
using HomeLiveStore.Infrastructure;
using HomeLiveStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Api
{
  public static class StoreApiExtensions
  {
    public static IServiceCollection AddStoreApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
      services.AddProblemDetails();
      services.AddExceptionHandler<StoreExceptionHandler>();
      services.AddAntiforgery(options =>
      {
        options.HeaderName = "X-CSRF-TOKEN";
        options.Cookie.Name = "HomeLiveStore.Csrf";
      });

      services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
          options.Cookie.Name = "HomeLiveStore.Auth";
          options.LoginPath = "/Account/Login";
          options.AccessDeniedPath = "/Account/Login";
          options.Events = new CookieAuthenticationEvents
          {
            OnRedirectToLogin = context =>
            {
              if (IsApiRequest(context.Request))
              {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
              }

              context.Response.Redirect(context.RedirectUri);
              return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
              if (IsApiRequest(context.Request) || context.Request.Path.StartsWithSegments("/Admin"))
              {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
              }

              context.Response.Redirect(context.RedirectUri);
              return Task.CompletedTask;
            }
          };
        });

      services.AddAuthorization();
      services.AddOpenApi();
      services.AddHomeLiveStoreApplication();
      services.AddHomeLiveStoreInfrastructure(configuration, environment);
      services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
        .AddApplicationPart(typeof(StoreApiExtensions).Assembly)
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

      return services;
    }

    public static WebApplication UseStoreApi(this WebApplication app)
    {
      app.UseExceptionHandler();
      app.UseStatusCodePages();

      if (app.Environment.IsDevelopment())
      {
        app.MapOpenApi();
      }

      app.UseAuthentication();
      app.UseAuthorization();
      app.MapControllers();
      app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
      return app;
    }

    public static async Task InitializeStoreDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
      await using var scope = app.Services.CreateAsyncScope();
      var initializer = scope.ServiceProvider.GetRequiredService<IStoreDatabaseInitializer>();
      await initializer.InitializeAsync(cancellationToken);
    }

    internal static ClaimsPrincipal CreatePrincipal(Application.DTOs.UserResponse user)
    {
      var claims = new[]
      {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, $"{user.Name} {user.Surname}".Trim()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
      };

      return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static bool IsApiRequest(HttpRequest request) => request.Path.StartsWithSegments("/api");
  }

  internal sealed class StoreExceptionHandler(ILogger<StoreExceptionHandler> logger) : IExceptionHandler
  {
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
      var (status, title) = exception switch
      {
        StoreValidationException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
        StoreUnauthorizedException => (StatusCodes.Status401Unauthorized, "Yetkisiz"),
        StoreNotFoundException => (StatusCodes.Status404NotFound, "Bulunamadı"),
        StoreConflictException => (StatusCodes.Status409Conflict, "İşlem yapılamadı"),
        _ => (0, string.Empty)
      };

      if (status == 0)
      {
        return false;
      }

      logger.LogWarning(exception, "Handled store exception");
      httpContext.Response.StatusCode = status;
      await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
      {
        Status = status,
        Title = title,
        Detail = exception.Message,
        Instance = httpContext.Request.Path
      }, cancellationToken);

      return true;
    }
  }
}
