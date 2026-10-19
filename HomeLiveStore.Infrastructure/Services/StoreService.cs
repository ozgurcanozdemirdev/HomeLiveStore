using HomeLiveStore.Application.DTOs;
using HomeLiveStore.Application.Exceptions;
using HomeLiveStore.Application.Interfaces.Services;
using HomeLiveStore.Domain.Entities;
using HomeLiveStore.Domain.Enums;
using HomeLiveStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeLiveStore.Infrastructure.Services
{
  public sealed class StoreService(HomeLiveStoreDbContext db, IPasswordHasher<User> passwordHasher) :
    IAuthService,
    ICatalogService,
    IOrderService,
    IDashboardService
  {
    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
      var email = NormalizeEmail(request.Email);
      if (await db.Users.AnyAsync(x => x.EMail == email && !x.Deleted, cancellationToken))
      {
        throw new StoreConflictException("Bu e-posta zaten kayıtlı.");
      }

      var user = new User
      {
        Name = request.Name.Trim(),
        Surname = request.Surname.Trim(),
        EMail = email,
        UserRole = UserRole.Customer
      };
      user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

      db.Users.Add(user);
      try
      {
        await db.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException)
      {
        throw new StoreConflictException("Bu e-posta zaten kayıtlı.");
      }
      return MapUser(user);
    }

    public async Task<UserResponse> ValidateLoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
      var email = NormalizeEmail(request.Email);
      var user = await db.Users.FirstOrDefaultAsync(x => x.EMail == email && x.Active && !x.Deleted, cancellationToken);
      if (user is null)
      {
        throw new StoreUnauthorizedException("E-posta veya parola hatalı.");
      }

      var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
      if (verification == PasswordVerificationResult.Failed)
      {
        throw new StoreUnauthorizedException("E-posta veya parola hatalı.");
      }

      return MapUser(user);
    }

    public async Task<UserResponse> GetUserAsync(int userId, CancellationToken cancellationToken = default)
    {
      var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId && x.Active && !x.Deleted, cancellationToken);
      return user is null ? throw new StoreNotFoundException("Kullanıcı bulunamadı.") : MapUser(user);
    }

    public async Task<IReadOnlyList<UserListDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
      return await db.Users.AsNoTracking()
        .Where(x => !x.Deleted)
        .OrderBy(x => x.Name)
        .ThenBy(x => x.Surname)
        .Select(x => new UserListDto(x.Id, x.Name, x.Surname, x.EMail, x.UserRole.ToString()))
        .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryListDto>> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
      return await db.ProductCategory.AsNoTracking()
        .Where(x => !x.Deleted && (includeInactive || x.Active))
        .OrderBy(x => x.Definition)
        .Select(x => new CategoryListDto(x.Id, x.Definition, x.Description, x.Active))
        .ToListAsync(cancellationToken);
    }

    public async Task<CategoryListDto> CreateCategoryAsync(CategorySaveDto request, CancellationToken cancellationToken = default)
    {
      var category = new ProductCategory
      {
        Definition = request.Definition.Trim(),
        Description = request.Description?.Trim() ?? string.Empty,
        Active = request.IsActive
      };

      db.ProductCategory.Add(category);
      await db.SaveChangesAsync(cancellationToken);
      return MapCategory(category);
    }

    public async Task<CategoryListDto> UpdateCategoryAsync(int id, CategorySaveDto request, CancellationToken cancellationToken = default)
    {
      var category = await db.ProductCategory.FirstOrDefaultAsync(x => x.Id == id && !x.Deleted, cancellationToken)
        ?? throw new StoreNotFoundException("Kategori bulunamadı.");

      category.Definition = request.Definition.Trim();
      category.Description = request.Description?.Trim() ?? string.Empty;
      category.Active = request.IsActive;
      category.LastModifedOn = DateTimeOffset.UtcNow;
      await db.SaveChangesAsync(cancellationToken);
      return MapCategory(category);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
      var category = await db.ProductCategory.FirstOrDefaultAsync(x => x.Id == id && !x.Deleted, cancellationToken)
        ?? throw new StoreNotFoundException("Kategori bulunamadı.");

      var hasProducts = await db.Products.AnyAsync(x => x.ProductCategoryId == id && !x.Deleted, cancellationToken);
      if (hasProducts)
      {
        throw new StoreConflictException("Ürünü olan kategori silinemez.");
      }

      category.Deleted = true;
      category.Active = false;
      category.LastModifedOn = DateTimeOffset.UtcNow;
      await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductPageResponse> GetProductsAsync(string search, int categoryId, string sort, int page, int pageSize, bool includeInactive, CancellationToken cancellationToken = default)
    {
      page = Math.Max(1, page);
      pageSize = Math.Clamp(pageSize, 1, 60);
      search = search?.Trim() ?? string.Empty;
      sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim();

      var query = db.Products.AsNoTracking()
        .Include(x => x.ProductCategory)
        .Include(x => x.ProductImages)
        .Where(x => !x.Deleted && (includeInactive || (x.Active && x.ProductCategory.Active)));

      if (!string.IsNullOrWhiteSpace(search))
      {
        db.EnableProductSearch();
        query = query.Where(x => HomeLiveStoreDbContext.MatchesSearch(x.Definition, search)
          || HomeLiveStoreDbContext.MatchesSearch(x.Description, search)
          || HomeLiveStoreDbContext.MatchesSearch(x.Barcode, search));
      }

      if (categoryId > 0)
      {
        query = query.Where(x => x.ProductCategoryId == categoryId);
      }

      var allItems = await query.ToListAsync(cancellationToken);
      var ordered = sort switch
      {
        "priceAsc" => allItems.OrderBy(x => x.UnitPrice).ThenBy(x => x.Definition),
        "priceDesc" => allItems.OrderByDescending(x => x.UnitPrice).ThenBy(x => x.Definition),
        "name" => allItems.OrderBy(x => x.Definition),
        _ => allItems.OrderByDescending(x => x.CreatedOn).ThenBy(x => x.Definition)
      };

      var totalCount = allItems.Count;
      var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(MapProduct).ToList();
      return new ProductPageResponse(items, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ProductListDto> GetProductAsync(int id, bool includeInactive, CancellationToken cancellationToken = default)
    {
      var product = await db.Products.AsNoTracking()
        .Include(x => x.ProductCategory)
        .Include(x => x.ProductImages)
        .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted && (includeInactive || (x.Active && x.ProductCategory.Active)), cancellationToken);

      return product is null ? throw new StoreNotFoundException("Ürün bulunamadı.") : MapProduct(product);
    }

    public async Task<ProductListDto> CreateProductAsync(ProductSaveDto request, CancellationToken cancellationToken = default)
    {
      await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
      var product = new Product();
      ApplyProduct(product, request);
      db.Products.Add(product);
      try
      {
        await db.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException)
      {
        throw new StoreConflictException("Bu barkod zaten kayıtlı.");
      }
      return await GetProductAsync(product.Id, true, cancellationToken);
    }

    public async Task<ProductListDto> UpdateProductAsync(int id, ProductSaveDto request, CancellationToken cancellationToken = default)
    {
      if (request.OriginalStockAmount is null)
      {
        throw new StoreValidationException("Ürün güncellenirken ilk stok miktarı gönderilmelidir.");
      }

      // Stok kontrolü ve kayıt aynı işlemde kalır; araya başka bir satış yazılamaz.
      await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
      await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
      var product = await db.Products.Include(x => x.ProductImages).FirstOrDefaultAsync(x => x.Id == id && !x.Deleted, cancellationToken)
        ?? throw new StoreNotFoundException("Ürün bulunamadı.");

      var stockChanged = request.StockAmount != request.OriginalStockAmount.Value;
      if (stockChanged && product.StockAmount != request.OriginalStockAmount.Value)
      {
        throw new StoreConflictException("Stok siz düzenleme yaparken değişti. Ürünü yeniden açıp güncel stokla tekrar deneyin.");
      }

      // Stok alanına dokunulmadıysa formdaki eski değer yerine güncel stok korunur.
      var stockAmount = stockChanged ? request.StockAmount : product.StockAmount;
      ApplyProduct(product, request with { StockAmount = stockAmount });
      product.LastModifedOn = DateTimeOffset.UtcNow;
      try
      {
        await db.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException)
      {
        throw new StoreConflictException("Bu barkod zaten kayıtlı.");
      }
      await transaction.CommitAsync(cancellationToken);
      return await GetProductAsync(product.Id, true, cancellationToken);
    }

    public async Task DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
      var product = await db.Products.FirstOrDefaultAsync(x => x.Id == id && !x.Deleted, cancellationToken)
        ?? throw new StoreNotFoundException("Ürün bulunamadı.");

      product.Deleted = true;
      product.Active = false;
      product.LastModifedOn = DateTimeOffset.UtcNow;
      await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrderResponse> CreateOrderAsync(int userId, OrderCreateRequest request, CancellationToken cancellationToken = default)
    {
      if (request.RequestId == Guid.Empty)
      {
        throw new StoreValidationException("requestId zorunludur.");
      }

      if (request.OrderLines is null || request.OrderLines.Any(line => line is null))
      {
        throw new StoreValidationException("Sipariş satırı boş olamaz.");
      }

      var existing = await LoadOrderQuery().FirstOrDefaultAsync(x => x.UserId == userId && x.RequestId == request.RequestId.ToString(), cancellationToken);
      if (existing is not null)
      {
        return MapOrder(existing);
      }

      var groupedLines = request.OrderLines
        .GroupBy(x => x.ProductId)
        .Select(x => new { ProductId = x.Key, Quantity = x.Sum(line => (long)line.Quantity) })
        .ToList();

      // Aynı ürün farklı satırlarda gönderilse de toplam adet aynı sınıra tabidir.
      if (groupedLines.Count == 0 || groupedLines.Any(x => x.Quantity < 1 || x.Quantity > BasketLineRequest.MaximumQuantity))
      {
        throw new StoreValidationException($"Bir ürün için sipariş adedi 1 ile {BasketLineRequest.MaximumQuantity} arasında olmalıdır.");
      }
      var mergedLines = groupedLines.Select(line => new { line.ProductId, Quantity = (int)line.Quantity }).ToList();

      await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

      existing = await LoadOrderQuery().FirstOrDefaultAsync(x => x.UserId == userId && x.RequestId == request.RequestId.ToString(), cancellationToken);
      if (existing is not null)
      {
        await transaction.CommitAsync(cancellationToken);
        return MapOrder(existing);
      }

      var ids = mergedLines.Select(x => x.ProductId).ToList();
      var products = await db.Products.AsNoTracking()
        .Include(x => x.ProductCategory)
        .Where(x => ids.Contains(x.Id) && x.Active && !x.Deleted && x.ProductCategory.Active)
        .ToDictionaryAsync(x => x.Id, cancellationToken);

      if (products.Count != ids.Count)
      {
        throw new StoreValidationException("Sepette geçersiz ürün var.");
      }

      foreach (var line in mergedLines)
      {
        var affected = await db.Products
          .Where(x => x.Id == line.ProductId && x.StockAmount >= line.Quantity && x.Active && !x.Deleted && x.ProductCategory.Active)
          .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.StockAmount, x => x.StockAmount - line.Quantity), cancellationToken);

        if (affected != 1)
        {
          throw new StoreConflictException("Stok yetersiz.");
        }
      }

      var order = new Order
      {
        UserId = userId,
        RequestId = request.RequestId.ToString(),
        DeliveryAdress = request.DeliveryAddress.Trim(),
        OrderDate = DateTimeOffset.UtcNow,
        OrderStatus = OrderStatus.Preparing
      };

      foreach (var line in mergedLines)
      {
        order.SatirEkle(products[line.ProductId], line.Quantity);
      }

      db.Orders.Add(order);
      await db.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      return MapOrder(await LoadOrderAsync(order.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<OrderResponse>> GetOrdersAsync(int userId, bool includeAll, CancellationToken cancellationToken = default)
    {
      var query = LoadOrderQuery();
      if (!includeAll)
      {
        query = query.Where(x => x.UserId == userId);
      }

      var orders = await query.ToListAsync(cancellationToken);
      return orders.OrderByDescending(x => x.OrderDate).Select(MapOrder).ToList();
    }

    public async Task<OrderResponse> GetOrderAsync(int orderId, int userId, bool includeAll, CancellationToken cancellationToken = default)
    {
      var query = LoadOrderQuery().Where(x => x.Id == orderId);
      if (!includeAll)
      {
        query = query.Where(x => x.UserId == userId);
      }

      var order = await query.FirstOrDefaultAsync(cancellationToken);
      return order is null ? throw new StoreNotFoundException("Siparis bulunamadi.") : MapOrder(order);
    }

    public async Task<OrderResponse> CancelOrderAsync(int orderId, int userId, bool isManager, CancellationToken cancellationToken = default)
    {
      await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
      var order = await LoadTrackedOrderAsync(orderId, cancellationToken);
      if (!isManager && order.UserId != userId)
      {
        throw new StoreNotFoundException("Siparis bulunamadi.");
      }

      if (order.OrderStatus == OrderStatus.Canceled)
      {
        await transaction.CommitAsync(cancellationToken);
        return MapOrder(order);
      }

      if (order.OrderStatus != OrderStatus.Preparing)
      {
        throw new StoreConflictException("Bu siparis iptal edilemez.");
      }

      await ReturnStockAsync(order, cancellationToken);
      order.OrderStatus = OrderStatus.Canceled;
      order.LastModifedOn = DateTimeOffset.UtcNow;
      await db.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
      return MapOrder(await LoadOrderAsync(order.Id, cancellationToken));
    }

    public async Task<OrderResponse> UpdateStatusAsync(int orderId, OrderStatus status, CancellationToken cancellationToken = default)
    {
      await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
      var order = await LoadTrackedOrderAsync(orderId, cancellationToken);
      if (order.OrderStatus == status)
      {
        await transaction.CommitAsync(cancellationToken);
        return MapOrder(order);
      }

      if (!IsValidTransition(order.OrderStatus, status))
      {
        throw new StoreConflictException("Siparis durumu bu sekilde degistirilemez.");
      }

      if (status == OrderStatus.Canceled)
      {
        await ReturnStockAsync(order, cancellationToken);
      }

      order.OrderStatus = status;
      order.LastModifedOn = DateTimeOffset.UtcNow;
      await db.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
      return MapOrder(await LoadOrderAsync(order.Id, cancellationToken));
    }

    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
      var products = await db.Products.AsNoTracking().Include(x => x.ProductCategory).Where(x => !x.Deleted).ToListAsync(cancellationToken);
      var orders = await LoadOrderQuery().ToListAsync(cancellationToken);
      var activeOrders = orders.Where(x => x.OrderStatus != OrderStatus.Canceled).ToList();

      return new DashboardResponse(
        products.Count,
        await db.ProductCategory.CountAsync(x => !x.Deleted, cancellationToken),
        await db.Users.CountAsync(x => !x.Deleted && x.UserRole == UserRole.Customer, cancellationToken),
        orders.Count,
        orders.Count(x => x.OrderStatus == OrderStatus.Preparing),
        activeOrders.Sum(x => x.TotalAmount),
        products.OrderBy(x => x.StockAmount).Take(5).Select(MapProduct).ToList(),
        orders.OrderByDescending(x => x.OrderDate).Take(5).Select(MapOrder).ToList());
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
      if (!await db.ProductCategory.AnyAsync(x => x.Id == categoryId && !x.Deleted, cancellationToken))
      {
        throw new StoreValidationException("Kategori bulunamadi.");
      }
    }

    private void ApplyProduct(Product product, ProductSaveDto request)
    {
      // Galeri adreslerini önce doğruluyoruz; hatalı bir adres kaydın bir kısmını değiştiremez.
      List<string>? imagePaths = null;
      if (request.ImagePaths is not null)
      {
        if (request.ImagePaths.Count > 10)
          throw new StoreValidationException("Kapak görseline ek olarak en fazla 10 görsel ekleyebilirsiniz.");

        imagePaths = request.ImagePaths.Select(path =>
        {
          if (string.IsNullOrWhiteSpace(path))
            throw new StoreValidationException("Ek görsel adresi boş bırakılamaz.");
          return ValidateImagePath(path);
        }).Distinct(StringComparer.Ordinal).ToList();
      }

      product.ProductCategoryId = request.CategoryId;
      product.Definition = request.Definition.Trim();
      product.Description = request.Description?.Trim() ?? string.Empty;
      product.Barcode = request.Barcode.Trim();
      product.Price = request.Price;
      product.VatRate = request.VatRate;
      product.StockAmount = request.StockAmount;
      product.CoverPhoto = ValidateImagePath(request.CoverPhoto);
      product.Active = request.IsActive;

      if (imagePaths is not null)
      {
        // İstek sırası yeni kayıtların kimlik sırasına dönüşür; galeri her açılışta aynı sırada gelir.
        db.ProductImages.RemoveRange(product.ProductImages);
        product.ProductImages = imagePaths.Select(path => new ProductImage { Path = path }).ToList();
      }
    }

    private static string ValidateImagePath(string? input)
    {
      var value = input?.Trim() ?? string.Empty;
      if (value.Length == 0) return value;
      if (value.Length > 400)
        throw new StoreValidationException("Görsel adresi en fazla 400 karakter olabilir.");

      var localImage = value.StartsWith("/images/", StringComparison.Ordinal)
        && value.Split('/').Skip(2).All(segment => segment.Length > 0 && segment is not "." and not ".."
          && segment.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'));
      var httpsImage = Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);

      if (value.Any(char.IsControl) || (!localImage && !httpsImage))
        throw new StoreValidationException("Görsel adresi /images/ altında bir dosya veya geçerli bir HTTPS adresi olmalıdır.");

      return value;
    }

    private async Task<Order> LoadOrderAsync(int id, CancellationToken cancellationToken)
    {
      return await LoadOrderQuery().FirstAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<Order> LoadTrackedOrderAsync(int id, CancellationToken cancellationToken)
    {
      return await db.Orders
        .Include(x => x.User)
        .Include(x => x.OrderLines)
        .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted, cancellationToken)
        ?? throw new StoreNotFoundException("Siparis bulunamadi.");
    }

    private IQueryable<Order> LoadOrderQuery()
    {
      return db.Orders.AsNoTracking()
        .Include(x => x.User)
        .Include(x => x.OrderLines)
        .Where(x => !x.Deleted);
    }

    private async Task ReturnStockAsync(Order order, CancellationToken cancellationToken)
    {
      foreach (var line in order.OrderLines)
      {
        // Sınır kontrolü ve artış aynı SQL işlemindedir. Bir satır bile sığmazsa
        // çağıranın transaction'ı önceki stok iadelerini ve iptali geri alır.
        var affected = await db.Products
          .Where(x => x.Id == line.ProductId && x.StockAmount <= int.MaxValue - line.Quantity)
          .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.StockAmount, x => x.StockAmount + line.Quantity), cancellationToken);
        if (affected != 1)
        {
          throw new StoreConflictException("Stok iadesi üst sınırı aşacağı için sipariş iptal edilemedi. Ürünün stok miktarını kontrol edip tekrar deneyin.");
        }
      }
    }

    private static bool IsValidTransition(OrderStatus current, OrderStatus next)
    {
      return current == OrderStatus.Preparing && (next == OrderStatus.Shipped || next == OrderStatus.Canceled)
        || current == OrderStatus.Shipped && next == OrderStatus.Completed;
    }

    private static UserResponse MapUser(User user) => new(user.Id, user.Name, user.Surname, user.EMail, user.UserRole.ToString());

    private static CategoryListDto MapCategory(ProductCategory category) => new(category.Id, category.Definition, category.Description, category.Active);

    private static ProductListDto MapProduct(Product product) => new(
      product.Id,
      product.ProductCategoryId,
      product.ProductCategory is null ? string.Empty : product.ProductCategory.Definition,
      product.Definition,
      product.Description,
      product.Barcode,
      product.Price,
      product.VatRate,
      product.UnitPrice,
      product.StockAmount,
      product.CoverPhoto,
      product.Active)
    {
      ImagePaths = product.ProductImages.Where(x => x.Active && !x.Deleted).OrderBy(x => x.Id).Select(x => x.Path).ToList()
    };

    private static OrderResponse MapOrder(Order order) => new(
      order.Id,
      order.OrderDate,
      order.TotalAmount,
      order.DeliveryAdress,
      order.OrderStatus,
      order.User is null ? string.Empty : $"{order.User.Name} {order.User.Surname}".Trim(),
      order.User is null ? string.Empty : order.User.EMail,
      order.OrderLines.OrderBy(x => x.Id).Select(line => new OrderLineResponse(line.ProductId, line.ProductName, line.UnitPrice, line.Quantity, line.LineTotalAmount)).ToList());

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
  }
}
