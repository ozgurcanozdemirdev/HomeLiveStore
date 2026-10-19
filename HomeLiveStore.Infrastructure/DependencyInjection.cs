using HomeLiveStore.Application.Interfaces.Services;
using HomeLiveStore.Infrastructure.Persistence;
using HomeLiveStore.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HomeLiveStore.Infrastructure
{
  public static class DependencyInjection
  {
    public static IServiceCollection AddHomeLiveStoreInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
      var connectionString = configuration.GetConnectionString("Store");
      if (string.IsNullOrWhiteSpace(connectionString))
      {
        connectionString = $"Data Source={GetDefaultDatabasePath()}";
      }

      services.AddDbContext<HomeLiveStoreDbContext>(options => options.UseSqlite(connectionString));
      services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
      services.AddScoped<IUnitOfWork, UnitOfWork>();
      services.AddScoped<IPasswordHasher<Domain.Entities.User>, PasswordHasher<Domain.Entities.User>>();
      services.AddScoped<IAuthService, StoreService>();
      services.AddScoped<ICatalogService, StoreService>();
      services.AddScoped<IOrderService, StoreService>();
      services.AddScoped<IDashboardService, StoreService>();
      services.AddScoped<IStoreDatabaseInitializer, StoreDatabaseInitializer>();
      return services;
    }

    private static string GetDefaultDatabasePath()
    {
      var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
      while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HomeLiveStore.slnx")))
      {
        directory = directory.Parent;
      }

      var root = directory?.FullName ?? Directory.GetCurrentDirectory();
      var dataDirectory = Path.Combine(root, "App_Data");
      Directory.CreateDirectory(dataDirectory);
      return Path.Combine(dataDirectory, "store.db");
    }
  }
}
