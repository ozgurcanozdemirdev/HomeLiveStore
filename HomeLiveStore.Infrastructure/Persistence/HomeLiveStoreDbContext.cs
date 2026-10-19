using HomeLiveStore.Domain.Entities;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeLiveStore.Infrastructure.Persistence
{
  public class HomeLiveStoreDbContext(DbContextOptions<HomeLiveStoreDbContext> options) : DbContext(options)
  {
    private static readonly CompareInfo SearchComparison = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategory => Set<ProductCategory>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public void EnableProductSearch()
    {
      // SQLite'ın yerleşik harf dönüşümü Türkçe karakterleri kapsamaz. Filtreyi yine
      // SQL içinde çalıştırıp I/ı, İ/i ve barkodlardaki ASCII harflerini eşleştiriyoruz.
      var connection = (SqliteConnection)Database.GetDbConnection();
      connection.CreateFunction("store_contains", (string value, string search) =>
        SearchComparison.IndexOf(value ?? string.Empty, search ?? string.Empty, CompareOptions.IgnoreCase) >= 0
        || (value ?? string.Empty).Contains(search ?? string.Empty, StringComparison.OrdinalIgnoreCase), isDeterministic: true);
    }

    public static bool MatchesSearch(string value, string search) => throw new NotSupportedException("Bu işlev yalnızca SQL sorgusunda kullanılır.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);
      modelBuilder.HasDbFunction(typeof(HomeLiveStoreDbContext).GetMethod(nameof(MatchesSearch))!).HasName("store_contains");

      modelBuilder.Entity<User>(entity =>
      {
        entity.HasIndex(x => x.EMail).IsUnique();
        entity.Property(x => x.EMail).HasMaxLength(256).IsRequired();
        entity.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Surname).HasMaxLength(100).IsRequired();
      });

      modelBuilder.Entity<ProductCategory>(entity =>
      {
        entity.Property(x => x.Definition).HasMaxLength(150).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(500);
      });

      modelBuilder.Entity<Product>(entity =>
      {
        entity.Property(x => x.Definition).HasMaxLength(250).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(1500);
        entity.Property(x => x.Barcode).HasMaxLength(30).IsRequired();
        entity.Property(x => x.CoverPhoto).HasMaxLength(400);
        entity.HasIndex(x => x.Barcode).IsUnique();
        entity.HasOne(x => x.ProductCategory).WithMany(x => x.Products).HasForeignKey(x => x.ProductCategoryId);
      });

      modelBuilder.Entity<Order>(entity =>
      {
        entity.Property(x => x.DeliveryAdress).HasMaxLength(400).IsRequired();
        entity.Property(x => x.RequestId).HasMaxLength(64).IsRequired();
        entity.HasIndex(x => new { x.UserId, x.RequestId }).IsUnique();
        entity.HasOne(x => x.User).WithMany(x => x.Orders).HasForeignKey(x => x.UserId);
      });

      modelBuilder.Entity<OrderLine>(entity =>
      {
        entity.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
        entity.HasOne(x => x.Order).WithMany(x => x.OrderLines).HasForeignKey(x => x.OrderId);
        entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
      });

      modelBuilder.Entity<ProductImage>(entity =>
      {
        entity.Property(x => x.Path).HasMaxLength(400).IsRequired();
        entity.HasOne(x => x.Product).WithMany(x => x.ProductImages).HasForeignKey(x => x.ProductId);
      });
    }
  }
}
