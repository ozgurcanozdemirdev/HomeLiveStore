using HomeLiveStore.Domain.Entities;
using HomeLiveStore.Domain.Enums;
using HomeLiveStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HomeLiveStore.Infrastructure.Services
{
  public sealed class StoreDatabaseInitializer(HomeLiveStoreDbContext db, IHostEnvironment environment, IPasswordHasher<User> passwordHasher) : IStoreDatabaseInitializer
  {
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
      await db.Database.EnsureCreatedAsync(cancellationToken);

      if (!environment.IsDevelopment())
      {
        return;
      }

      if (!await db.ProductCategory.AnyAsync(cancellationToken))
      {
        var categories = new[]
        {
          new ProductCategory { Definition = "Ev Yaşam", Description = "Günlük yaşam ürünleri" },
          new ProductCategory { Definition = "Ofis", Description = "Çalışma alanı ürünleri" },
          new ProductCategory { Definition = "Mutfak", Description = "Mutfak ve servis ürünleri" }
        };

        db.ProductCategory.AddRange(categories);
        await db.SaveChangesAsync(cancellationToken);

        db.Products.AddRange(
          new Product { ProductCategoryId = categories[0].Id, Definition = "Pamuk Battaniye", Description = "Yumuşak dokulu pamuk battaniye", Barcode = "EV-1001", Price = 850m, VatRate = 20, StockAmount = 18, CoverPhoto = "/images/products/blanket.svg" },
          new Product { ProductCategoryId = categories[0].Id, Definition = "Seramik Vazo", Description = "Mat yüzeyli dekoratif vazo", Barcode = "EV-1002", Price = 420m, VatRate = 20, StockAmount = 9, CoverPhoto = "/images/products/vase.svg" },
          new Product { ProductCategoryId = categories[1].Id, Definition = "Masa Lambası", Description = "Ayarlanabilir led masa lambası", Barcode = "OF-2001", Price = 690m, VatRate = 20, StockAmount = 12, CoverPhoto = "/images/products/lamp.svg" },
          new Product { ProductCategoryId = categories[1].Id, Definition = "Deri Defter", Description = "Çizgili sayfalı iş defteri", Barcode = "OF-2002", Price = 180m, VatRate = 10, StockAmount = 30, CoverPhoto = "/images/products/notebook.svg" },
          new Product { ProductCategoryId = categories[2].Id, Definition = "Bambu Kesme Tahtası", Description = "Doğal bambu servis ve kesme tahtası", Barcode = "MT-3001", Price = 310m, VatRate = 20, StockAmount = 16, CoverPhoto = "/images/products/board.svg" });
        await db.SaveChangesAsync(cancellationToken);
      }

      await SeedUserAsync("Admin", "Demo", "admin@homelivestore.local", "Admin123!", UserRole.Manager, cancellationToken);
      await SeedUserAsync("Müşteri", "Demo", "musteri@homelivestore.local", "Musteri123!", UserRole.Customer, cancellationToken);
      await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUserAsync(string name, string surname, string email, string password, UserRole role, CancellationToken cancellationToken)
    {
      if (await db.Users.AnyAsync(x => x.EMail == email, cancellationToken))
      {
        return;
      }

      var user = new User
      {
        Name = name,
        Surname = surname,
        EMail = email,
        UserRole = role
      };
      user.PasswordHash = passwordHasher.HashPassword(user, password);
      db.Users.Add(user);
    }
  }
}
