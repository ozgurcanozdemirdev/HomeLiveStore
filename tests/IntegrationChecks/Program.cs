using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

var address = args.FirstOrDefault(x => x.StartsWith("http", StringComparison.Ordinal)) ?? "http://127.0.0.1:5100";
var baseline = args.Contains("--baseline");
var checks = new List<(string Name, Func<Task> Run)>();
using var guest = new Client(address);
checks.Add(("Public catalogue API exists", async () => await guest.Get("/api/categories", 200)));
checks.Add(("Store catalogue page exists", async () => await guest.Get("/Products", 200)));
checks.Add(("Admin API requires authentication", async () => await guest.Get("/api/admin/dashboard", 401)));
if (baseline) return await RunChecks();

using var manager = new Client(address);
using var customer = new Client(address);
using var stranger = new Client(address);
var suffix = Guid.NewGuid().ToString("N")[..12];
var categoryId = 0;
var productId = 0;
var orderId = 0;
var concurrentOrderId = 0;
var requestId = Guid.NewGuid().ToString();
object Product(int stock = 5, decimal price = 100m, bool active = true, string image = "/images/product-placeholder.svg", int? originalStockAmount = null, string[]? imagePaths = null) => new {
    categoryId, definition = "Test Seramik Kupa " + suffix, description = "Test için üretilen ürün.",
    barcode = "TEST-" + suffix, price, vatRate = 20, stockAmount = stock, originalStockAmount,
    coverPhoto = image, isActive = active, imagePaths
};
object Order(int quantity, string? key = null) => new {
    deliveryAddress = "Örnek Mahallesi Ders Sokak No: 10 İstanbul", requestId = key ?? Guid.NewGuid().ToString(),
    orderLines = new[] { new { productId, quantity, unitPrice = 0.01m, productName = "Forged", lineTotalAmout = 0.01m } },
    totalAmount = 0.01m, userId = 1
};
checks.Add(("CSRF rejects unsafe unauthenticated request", async () => {
    await guest.Send(HttpMethod.Post, "/api/auth/login", new { email = "none@example.com", password = "wrong-password" }, 400, false);
}));
checks.Add(("Registration rejects invalid input", async () => {
    await guest.Send(HttpMethod.Post, "/api/auth/register", new { name = "", surname = "", email = "invalid", password = "1" }, 400);
}));
checks.Add(("Manager signs in with demo account", async () => {
    var me = await manager.Send(HttpMethod.Post, "/api/auth/login", new { email = "admin@homelivestore.local", password = "Admin123!" }, 200);
    Equal("Manager", me.GetProperty("role").GetString(), "manager role");
}));
checks.Add(("Manager can render all management pages", async () => {
    foreach (var route in new[] { "/Admin", "/Admin/Products", "/Admin/Categories", "/Admin/Orders", "/Admin/Users" })
        await manager.Get(route, 200);
}));
checks.Add(("Rendered admin navigation opens the users page", async () => {
    var html = await manager.GetText("/Admin");
    Assert(html.Contains("href=\"/Admin/Users\"", StringComparison.Ordinal), "user management navigation must open its HTML page");
}));
checks.Add(("Public and account pages render", async () => {
    foreach (var route in new[] { "/", "/Cart", "/Account/Login", "/Account/Register" })
        await guest.Get(route, 200);
}));
checks.Add(("Registration always grants Customer and establishes session", async () => {
    var me = await customer.Send(HttpMethod.Post, "/api/auth/register", new { name = "Ders", surname = "Öğrenci", email = $"student-{suffix}@example.com", password = "Student123!", role = "Manager" }, 201);
    Equal("Customer", me.GetProperty("role").GetString(), "customer role");
    var session = await customer.Get("/api/auth/me", 200);
    Equal(me.GetProperty("id").GetInt32(), session.GetProperty("id").GetInt32(), "session identity");
}));
checks.Add(("Duplicate normalized email cannot register", async () => {
    await guest.Send(HttpMethod.Post, "/api/auth/register", new { name = "Ders", surname = "Öğrenci", email = $" STUDENT-{suffix.ToUpperInvariant()}@EXAMPLE.COM ", password = "Student123!" }, 409);
}));
checks.Add(("Invalid password cannot establish session", async () => {
    await guest.Send(HttpMethod.Post, "/api/auth/login", new { email = $"student-{suffix}@example.com", password = "Wrong123!" }, 401);
    await guest.Get("/api/auth/me", 401);
}));
checks.Add(("Customer cannot administer store", async () => {
    await customer.Get("/api/admin/dashboard", 403);
    await customer.Send(HttpMethod.Post, "/api/admin/categories", new { definition = "Forbidden", description = "", isActive = true }, 403);
    await customer.Get("/Admin", 403);
}));
checks.Add(("Authorization and missing API routes return ProblemDetails", async () => {
    foreach (var (client, path, status) in new[] { (guest, "/api/auth/me", 401), (customer, "/api/admin/dashboard", 403), (guest, "/api/not-a-route", 404) })
    {
        var problem = await client.Get(path, status);
        Equal(status, problem.GetProperty("status").GetInt32(), "problem status");
        Assert(!string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()), "problem title");
    }
}));
checks.Add(("Manager creates category and product", async () => {
    var category = await manager.Send(HttpMethod.Post, "/api/admin/categories", new { definition = "Test " + suffix, description = "Test kategorisi", isActive = true }, 201);
    categoryId = category.GetProperty("id").GetInt32();
    var product = await manager.Send(HttpMethod.Post, "/api/admin/products", Product(), 201);
    productId = product.GetProperty("id").GetInt32();
    Equal(120m, product.GetProperty("unitPrice").GetDecimal(), "VAT inclusive unit price");
}));
checks.Add(("Catalogue filters and pagination return correct product", async () => {
    var page = await guest.Get($"/api/products?categoryId={categoryId}&search={suffix}&page=1&pageSize=1&sort=priceAsc", 200);
    Equal(1, page.GetProperty("totalCount").GetInt32(), "filtered count");
    Equal(productId, page.GetProperty("items")[0].GetProperty("id").GetInt32(), "filtered product");
}));
checks.Add(("Invalid price and stock rejected", async () => {
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(-1, originalStockAmount: 5), 400);
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(price: -1, originalStockAmount: 5), 400);
}));
checks.Add(("Category with product cannot be deleted", async () => await manager.Send(HttpMethod.Delete, $"/api/admin/categories/{categoryId}", null, 409)));
checks.Add(("Inactive category removes products from sale", async () => {
    await manager.Send(HttpMethod.Put, $"/api/admin/categories/{categoryId}", new { definition = "Test " + suffix, description = "Test kategorisi", isActive = false }, 200);
    await guest.Get($"/api/products/{productId}", 404);
    await customer.Send(HttpMethod.Post, "/api/orders", Order(1), 409, true, 400);
    await manager.Send(HttpMethod.Put, $"/api/admin/categories/{categoryId}", new { definition = "Test " + suffix, description = "Test kategorisi", isActive = true }, 200);
    await guest.Get($"/api/products/{productId}", 200);
}));
checks.Add(("Empty order and invalid quantities rejected", async () => {
    await customer.Send(HttpMethod.Post, "/api/orders", new { deliveryAddress = "Valid delivery address", requestId = Guid.NewGuid(), orderLines = Array.Empty<object>() }, 400);
    await customer.Send(HttpMethod.Post, "/api/orders", Order(0), 400);
    await customer.Send(HttpMethod.Post, "/api/orders", Order(-1), 400);
}));
checks.Add(("Insufficient stock leaves inventory unchanged", async () => {
    await customer.Send(HttpMethod.Post, "/api/orders", Order(6), 409);
    var product = await guest.Get($"/api/products/{productId}", 200);
    Equal(5, product.GetProperty("stockAmount").GetInt32(), "stock after rejection");
}));
checks.Add(("Null order lines return validation errors without stock changes", async () => {
    var before = (await customer.Get("/api/orders", 200)).GetArrayLength();
    var error = await customer.Send(HttpMethod.Post, "/api/orders", new {
        deliveryAddress = "Test Mahallesi Doğrulama Sokak No: 1 İstanbul", requestId = Guid.NewGuid(),
        orderLines = new object?[] { new { productId, quantity = 1 }, null }
    }, 400);
    Assert(error.TryGetProperty("errors", out _), "null line receives structured validation errors");
    Equal(before, (await customer.Get("/api/orders", 200)).GetArrayLength(), "invalid order is not saved");
    Equal(5, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "invalid order does not deduct stock");
}));
checks.Add(("Turkish product search matches case variants in names descriptions and barcodes", async () => {
    var product = await manager.Send(HttpMethod.Post, "/api/admin/products", new {
        categoryId, definition = "İnci Işık " + suffix, description = "Çalışma Köşesi Örme", barcode = "CASE-I-" + suffix,
        price = 10m, vatRate = 0, stockAmount = 2, coverPhoto = "", isActive = true
    }, 201);
    var searchProductId = product.GetProperty("id").GetInt32();
    foreach (var (upper, lower) in new[] { ("İNCİ", "inci"), ("IŞIK", "ışık"), ("ÇALIŞMA", "çalışma"), ("KÖŞESİ", "köşesi"), ("ÖRME", "örme"), ("CASE-I-" + suffix, "case-i-" + suffix) })
    {
        foreach (var search in new[] { upper, lower })
        {
            var page = await guest.Get($"/api/products?categoryId={categoryId}&search={Uri.EscapeDataString(search)}", 200);
            Assert(page.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetInt32() == searchProductId), "case-insensitive match for " + search);
        }
    }
    await manager.Send(HttpMethod.Delete, $"/api/admin/products/{searchProductId}", null, 204);
}));
checks.Add(("Order quantity limit applies to single and merged lines", async () => {
    var product = await manager.Send(HttpMethod.Post, "/api/admin/products", new {
        categoryId, definition = "Adet Sınırı " + suffix, description = "", barcode = "QTY-" + suffix,
        price = 1m, vatRate = 0, stockAmount = 1500, coverPhoto = "", isActive = true
    }, 201);
    var id = product.GetProperty("id").GetInt32();
    object QuantityOrder(params int[] quantities) => new {
        deliveryAddress = "Test Mahallesi Adet Sokak No: 1 İstanbul", requestId = Guid.NewGuid(),
        orderLines = quantities.Select(quantity => new { productId = id, quantity }).ToArray()
    };
    await customer.Send(HttpMethod.Post, "/api/orders", QuantityOrder(1000), 400);
    await customer.Send(HttpMethod.Post, "/api/orders", QuantityOrder(600, 600), 400);
    Equal(1500, (await guest.Get($"/api/products/{id}", 200)).GetProperty("stockAmount").GetInt32(), "excess quantities leave stock unchanged");
    var order = await customer.Send(HttpMethod.Post, "/api/orders", QuantityOrder(999), 201);
    Equal(999, order.GetProperty("orderLines")[0].GetProperty("quantity").GetInt32(), "maximum supported quantity succeeds");
    Equal(501, (await guest.Get($"/api/products/{id}", 200)).GetProperty("stockAmount").GetInt32(), "maximum quantity deducts once");
    await customer.Send(HttpMethod.Post, $"/api/orders/{order.GetProperty("id").GetInt32()}/cancel", null, 200);
    await manager.Send(HttpMethod.Delete, $"/api/admin/products/{id}", null, 204);
}));
checks.Add(("Order uses server price and deducts stock", async () => {
    var order = await customer.Send(HttpMethod.Post, "/api/orders", Order(2, requestId), 201);
    orderId = order.GetProperty("id").GetInt32();
    Equal(240m, order.GetProperty("totalAmount").GetDecimal(), "server total ignores forged price");
    Equal("Preparing", order.GetProperty("status").GetString(), "initial status");
    Equal(3, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "stock deduction");
}));
checks.Add(("Retrying identical order does not duplicate or deduct twice", async () => {
    var order = await customer.Send(HttpMethod.Post, "/api/orders", Order(2, requestId), 200, true, 201);
    Equal(orderId, order.GetProperty("id").GetInt32(), "idempotent order ID");
    Equal(3, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "stock after retry");
}));
checks.Add(("Other customer cannot read or cancel order", async () => {
    await stranger.Send(HttpMethod.Post, "/api/auth/register", new { name = "Diğer", surname = "Müşteri", email = $"other-{suffix}@example.com", password = "Student123!" }, 201);
    await stranger.Get($"/api/orders/{orderId}", 404);
    await stranger.Send(HttpMethod.Post, $"/api/orders/{orderId}/cancel", null, 404);
    Equal(0, (await stranger.Get("/api/orders", 200)).GetArrayLength(), "private history");
}));
checks.Add(("Order snapshots survive product edits", async () => {
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(3, 200m, originalStockAmount: 3), 200);
    Equal(240m, (await customer.Get($"/api/orders/{orderId}", 200)).GetProperty("totalAmount").GetDecimal(), "historical total");
}));
checks.Add(("Cancel restores stock exactly once", async () => {
    Equal("Canceled", (await customer.Send(HttpMethod.Post, $"/api/orders/{orderId}/cancel", null, 200)).GetProperty("status").GetString(), "canceled status");
    await customer.Send(HttpMethod.Post, $"/api/orders/{orderId}/cancel", null, 200, true, 409);
    Equal(5, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "restored inventory");
}));
checks.Add(("Order state machine disallows skips and reversals", async () => {
    var order = await customer.Send(HttpMethod.Post, "/api/orders", Order(1), 201);
    var id = order.GetProperty("id").GetInt32();
    await manager.Send(HttpMethod.Put, $"/api/admin/orders/{id}/status", new { status = "Completed" }, 409);
    await manager.Send(HttpMethod.Put, $"/api/admin/orders/{id}/status", new { status = "Shipped" }, 200);
    await customer.Send(HttpMethod.Post, $"/api/orders/{id}/cancel", null, 409);
    await manager.Send(HttpMethod.Put, $"/api/admin/orders/{id}/status", new { status = "Completed" }, 200);
    await manager.Send(HttpMethod.Put, $"/api/admin/orders/{id}/status", new { status = "Preparing" }, 409);
}));
checks.Add(("Admin dashboard and users do not disclose password hashes", async () => {
    var dashboard = await manager.Get("/api/admin/dashboard", 200);
    Assert(dashboard.GetProperty("orderCount").GetInt32() >= 2, "order dashboard count");
    var users = await manager.Get("/api/admin/users", 200);
    Assert(!users.GetRawText().Contains("password", StringComparison.OrdinalIgnoreCase), "no password material");
}));
checks.Add(("Combined duplicate lines cannot bypass stock validation", async () => {
    await customer.Send(HttpMethod.Post, "/api/orders", new {
        deliveryAddress = "Örnek Mahallesi No: 10 İstanbul", requestId = Guid.NewGuid(),
        orderLines = new[] { new { productId, quantity = 3 }, new { productId, quantity = 3 } }
    }, 409);
    Equal(4, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "stock after duplicate-line rejection");
}));
checks.Add(("Mixed valid and unavailable lines roll back all stock", async () => {
    await customer.Send(HttpMethod.Post, "/api/orders", new {
        deliveryAddress = "Örnek Mahallesi No: 10 İstanbul", requestId = Guid.NewGuid(),
        orderLines = new[] { new { productId, quantity = 1 }, new { productId = int.MaxValue, quantity = 1 } }
    }, 409, true, 400);
    Equal(4, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "all-or-nothing inventory");
}));
checks.Add(("Concurrent checkouts cannot oversell the final unit", async () => {
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(1, 200m, originalStockAmount: 4), 200);
    var responses = await Task.WhenAll(
        customer.Send(HttpMethod.Post, "/api/orders", Order(1), 201, true, 409),
        stranger.Send(HttpMethod.Post, "/api/orders", Order(1), 201, true, 409));
    Equal(1, responses.Count(x => x.TryGetProperty("id", out _)), "exactly one order succeeds");
    Equal(0, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "last unit sold once");
}));
checks.Add(("Eight simultaneous retries create one order and deduct once", async () => {
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(10, 200m, originalStockAmount: 0), 200);
    var key = Guid.NewGuid().ToString();
    var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => customer.Send(HttpMethod.Post, "/api/orders", Order(1, key), 201, true, 200)));
    Equal(1, responses.Select(x => x.GetProperty("id").GetInt32()).Distinct().Count(), "one order ID for all simultaneous retries");
    concurrentOrderId = responses[0].GetProperty("id").GetInt32();
    Equal(9, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("stockAmount").GetInt32(), "one inventory deduction");
}));
checks.Add(("Numeric order status is rejected by the string enum contract", async () => {
    await manager.Send(HttpMethod.Put, $"/api/admin/orders/{concurrentOrderId}/status", new { status = 2 }, 400);
}));
checks.Add(("Image paths follow the local images or HTTPS contract", async () => {
    foreach (var image in new[] { "http://example.com/image.png", "//example.com/image.png", "data:image/svg+xml,x", "/api/auth/logout", "/images/../api/auth/logout", "/images/%2e%2e/api/auth/logout" })
        await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, 200m, image: image, originalStockAmount: 9), 400);
    foreach (var image in new[] { "/images/products/vase.svg", "https://example.com/image.png", "" })
        await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, 200m, image: image, originalStockAmount: 9), 200);
}));
checks.Add(("Product galleries round-trip in order and remain manager-only", async () => {
    var photos = new[] { "/images/products/board.png", "/images/products/vase.png", "/images/products/board.png" };
    var saved = await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9, imagePaths: photos), 200);
    Equal(2, saved.GetProperty("imagePaths").GetArrayLength(), "duplicate image paths are removed");
    var publicProduct = await guest.Get($"/api/products/{productId}", 200);
    Equal(photos[0], publicProduct.GetProperty("imagePaths")[0].GetString(), "first photo persists");
    Equal(photos[1], publicProduct.GetProperty("imagePaths")[1].GetString(), "second photo persists");
    var adminPage = await manager.Get($"/api/admin/products?search=TEST-{suffix}", 200);
    Equal(2, adminPage.GetProperty("items")[0].GetProperty("imagePaths").GetArrayLength(), "admin edit receives saved photos");
    await guest.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9, imagePaths: Array.Empty<string>()), 401);
    await customer.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9, imagePaths: Array.Empty<string>()), 403);
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9), 200);
    Equal(2, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("imagePaths").GetArrayLength(), "omitted gallery is preserved");
    var reversed = new[] { photos[1], photos[0] };
    saved = await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9, imagePaths: reversed), 200);
    Equal(reversed[0], saved.GetProperty("imagePaths")[0].GetString(), "changed order persists");
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9, imagePaths: Array.Empty<string>()), 200);
    Equal(0, (await guest.Get($"/api/products/{productId}", 200)).GetProperty("imagePaths").GetArrayLength(), "empty gallery removes photos");
}));
checks.Add(("Gallery rejects invalid paths and limits without partially saving", async () => {
    var valid = new[] { "/images/products/board.png" };
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9, imagePaths: valid), 200);
    foreach (var photo in new[] { "", "javascript:alert(1)", "http://example.com/a.png", "//example.com/a.png", "/images/../api/auth/logout", "https://example.com/" + new string('a', 401) })
        await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, price: 500m, originalStockAmount: 9, imagePaths: new[] { valid[0], photo }), 400);
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(9, originalStockAmount: 9, imagePaths: Enumerable.Repeat(valid[0], 11).ToArray()), 400);
    var preserved = await guest.Get($"/api/products/{productId}", 200);
    Equal(100m, preserved.GetProperty("price").GetDecimal(), "invalid gallery does not partially save price");
    Equal(valid[0], preserved.GetProperty("imagePaths")[0].GetString(), "invalid gallery does not replace existing photos");
}));
checks.Add(("Inactive and deleted products are unavailable for ordering", async () => {
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{productId}", Product(4, active: false, originalStockAmount: 9), 200);
    await guest.Get($"/api/products/{productId}", 404);
    await customer.Send(HttpMethod.Post, "/api/orders", Order(1), 409, true, 400);
    await manager.Send(HttpMethod.Delete, $"/api/admin/products/{productId}", null, 204);
    await guest.Get($"/api/products/{productId}", 404);
    await manager.Send(HttpMethod.Delete, $"/api/admin/categories/{categoryId}", null, 204);
    Equal(240m, (await customer.Get($"/api/orders/{orderId}", 200)).GetProperty("totalAmount").GetDecimal(), "history after soft delete");
}));
checks.Add(("Stale product forms preserve sales and reject conflicting stock changes", async () => {
    var category = await manager.Send(HttpMethod.Post, "/api/admin/categories", new { definition = "Stok Koruma " + suffix, description = "", isActive = true }, 201);
    var stockCategoryId = category.GetProperty("id").GetInt32();
    object StockProduct(int stock, int? original = null, string name = "Stok Koruma Ürünü") => new {
        categoryId = stockCategoryId, definition = name, description = "", barcode = "STOCK-" + suffix,
        price = 100m, vatRate = 0, stockAmount = stock, originalStockAmount = original, coverPhoto = "", isActive = true
    };
    var product = await manager.Send(HttpMethod.Post, "/api/admin/products", StockProduct(5), 201);
    var id = product.GetProperty("id").GetInt32();
    await customer.Send(HttpMethod.Post, "/api/orders", new {
        deliveryAddress = "Test Mahallesi Stok Sokak No: 1 İstanbul", requestId = Guid.NewGuid(),
        orderLines = new[] { new { productId = id, quantity = 1 } }
    }, 201);

    // Form açıkken yapılan satış, yalnızca ürün adı kaydedildiğinde korunmalı.
    var edited = await manager.Send(HttpMethod.Put, $"/api/admin/products/{id}", StockProduct(5, 5, "Yeni ürün adı"), 200);
    Equal(4, edited.GetProperty("stockAmount").GetInt32(), "sale survives stale metadata edit");
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{id}", StockProduct(8), 400);
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{id}", StockProduct(8, 5), 409);
    Equal(4, (await guest.Get($"/api/products/{id}", 200)).GetProperty("stockAmount").GetInt32(), "rejected edits do not restore stock");
    Equal("Yeni ürün adı", (await guest.Get($"/api/products/{id}", 200)).GetProperty("definition").GetString(), "rejected edit does not change metadata");
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{id}", StockProduct(8, 4), 200);

    // Aynı başlangıç stoğunu kullanan iki yönetici birbirinin değişikliğini ezemez.
    var updates = await Task.WhenAll(
        manager.Send(HttpMethod.Put, $"/api/admin/products/{id}", StockProduct(9, 8), 200, true, 409),
        manager.Send(HttpMethod.Put, $"/api/admin/products/{id}", StockProduct(10, 8), 200, true, 409));
    Equal(1, updates.Count(x => x.TryGetProperty("id", out _)), "one of two competing stock edits succeeds");
    var currentStock = (await guest.Get($"/api/products/{id}", 200)).GetProperty("stockAmount").GetInt32();
    Assert(currentStock is 9 or 10, "successful stock adjustment is preserved");
    await manager.Send(HttpMethod.Delete, $"/api/admin/products/{id}", null, 204);
    await manager.Send(HttpMethod.Delete, $"/api/admin/categories/{stockCategoryId}", null, 204);
}));
checks.Add(("Stock return overflow rejects cancellation and rolls back every line", async () => {
    var category = await manager.Send(HttpMethod.Post, "/api/admin/categories", new { definition = "İade Sınırı " + suffix, description = "", isActive = true }, 201);
    var returnCategoryId = category.GetProperty("id").GetInt32();
    object ReturnProduct(string code, int stock, int? original = null) => new {
        categoryId = returnCategoryId, definition = "İade Ürünü " + code, description = "", barcode = code + suffix,
        price = 10m, vatRate = 0, stockAmount = stock, originalStockAmount = original, coverPhoto = "", isActive = true
    };
    var first = await manager.Send(HttpMethod.Post, "/api/admin/products", ReturnProduct("RET-A-", 1), 201);
    var second = await manager.Send(HttpMethod.Post, "/api/admin/products", ReturnProduct("RET-B-", 1), 201);
    var firstId = first.GetProperty("id").GetInt32();
    var secondId = second.GetProperty("id").GetInt32();
    var order = await customer.Send(HttpMethod.Post, "/api/orders", new {
        deliveryAddress = "Test Mahallesi İade Sokak No: 1 İstanbul", requestId = Guid.NewGuid(),
        orderLines = new[] { new { productId = firstId, quantity = 1 }, new { productId = secondId, quantity = 1 } }
    }, 201);
    var id = order.GetProperty("id").GetInt32();
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{secondId}", ReturnProduct("RET-B-", int.MaxValue, 0), 200);
    // İkinci satır sığmıyorsa ilk satırın iadesi ve sipariş durumu da geri alınmalıdır.
    await customer.Send(HttpMethod.Post, $"/api/orders/{id}/cancel", null, 409);
    await manager.Send(HttpMethod.Put, $"/api/admin/orders/{id}/status", new { status = "Canceled" }, 409);
    Equal(0, (await guest.Get($"/api/products/{firstId}", 200)).GetProperty("stockAmount").GetInt32(), "earlier stock return rolls back");
    Equal(int.MaxValue, (await guest.Get($"/api/products/{secondId}", 200)).GetProperty("stockAmount").GetInt32(), "overflow product stays within range");
    Equal("Preparing", (await customer.Get($"/api/orders/{id}", 200)).GetProperty("status").GetString(), "failed cancellation preserves order status");
    await guest.Get("/api/products", 200);
    await manager.Send(HttpMethod.Put, $"/api/admin/products/{secondId}", ReturnProduct("RET-B-", int.MaxValue - 1, int.MaxValue), 200);
    await customer.Send(HttpMethod.Post, $"/api/orders/{id}/cancel", null, 200);
    await customer.Send(HttpMethod.Post, $"/api/orders/{id}/cancel", null, 200);
    Equal(1, (await guest.Get($"/api/products/{firstId}", 200)).GetProperty("stockAmount").GetInt32(), "valid return applies exactly once");
    Equal(int.MaxValue, (await guest.Get($"/api/products/{secondId}", 200)).GetProperty("stockAmount").GetInt32(), "exact stock boundary remains readable");
    await manager.Send(HttpMethod.Delete, $"/api/admin/products/{firstId}", null, 204);
    await manager.Send(HttpMethod.Delete, $"/api/admin/products/{secondId}", null, 204);
    await manager.Send(HttpMethod.Delete, $"/api/admin/categories/{returnCategoryId}", null, 204);
}));
checks.Add(("Logout removes access", async () => {
    await customer.Send(HttpMethod.Post, "/api/auth/logout", null, 204);
    await customer.Get("/api/auth/me", 401);
    await customer.Get("/api/orders", 401);
}));
return await RunChecks();

async Task<int> RunChecks()
{
    var failures = 0;
    var passed = 0;
    foreach (var (name, run) in checks)
    {
        try { await run(); passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); if (!baseline) break; }
    }
    Console.WriteLine($"{(failures == 0 ? "PASS" : "FAIL")}: {passed} passed, {failures} failed, {checks.Count - passed - failures} not run.");
    return failures == 0 ? 0 : 1;
}
static void Equal<T>(T expected, T actual, string message) => Assert(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected {expected}, got {actual}");
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

sealed class Client : IDisposable
{
    private readonly HttpClient http;
    public Client(string address) => http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(20) };
    public Task<JsonElement> Get(string path, int expected) => Send(HttpMethod.Get, path, null, expected, false);
    public Task<string> GetText(string path) => http.GetStringAsync(path);
    public async Task<JsonElement> Send(HttpMethod method, string path, object? data, int expected, bool csrf = true, int? alternative = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (data is not null) request.Content = JsonContent.Create(data);
        if (csrf && method != HttpMethod.Get)
        {
            var token = await Get("/api/auth/csrf", 200);
            request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        }
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode != expected && (int)response.StatusCode != alternative)
            throw new InvalidOperationException($"{method} {path}: expected {expected}, got {(int)response.StatusCode}: {text[..Math.Min(text.Length, 700)]}");
        if (string.IsNullOrWhiteSpace(text) || response.Content.Headers.ContentType?.MediaType?.Contains("json") != true) return default;
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    public void Dispose() => http.Dispose();
}
