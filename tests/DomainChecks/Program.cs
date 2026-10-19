using HomeLiveStore.Domain.Entities;
using HomeLiveStore.Application.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

var checks = new List<(string Name, Action Check)>
{
    ("SatirEkle initializes order lines and accepts the first line", SatirEkleInitializesOrderLines),
    ("SatirEkle snapshots VAT-inclusive unit price", SatirEkleSnapshotsVatInclusiveUnitPrice),
    ("CalculateTotal keeps the stored price snapshot when product price changes", CalculateTotalKeepsStoredSnapshotWhenProductPriceChanges),
    ("Product price bounds validate under Turkish, English and German cultures", ProductPriceBoundsAreCultureIndependent),
    ("Order request validation rejects null collection entries", OrderRequestRejectsNullEntries),
    ("Order quantity validation accepts its boundary and rejects excess", OrderQuantityBounds)
};

var failures = new List<string>();

foreach (var (name, check) in checks)
{
    try
    {
        check();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"{failures.Count} domain check(s) failed.");
    Environment.Exit(1);
}

static void OrderRequestRejectsNullEntries()
{
    var request = new OrderCreateRequest
    {
        DeliveryAddress = "Örnek Mahallesi Test Sokak No: 1",
        RequestId = Guid.NewGuid(),
        OrderLines = new List<BasketLineRequest> { new() { ProductId = 1, Quantity = 1 }, null! }
    };
    var errors = new List<ValidationResult>();
    if (Validator.TryValidateObject(request, new ValidationContext(request), errors, true)
        || !errors.Any(error => error.MemberNames.Contains(nameof(OrderCreateRequest.OrderLines))))
    {
        throw new InvalidOperationException("Null order line was not reported as an OrderLines validation error.");
    }
}

static void OrderQuantityBounds()
{
    foreach (var (quantity, expected) in new[] { (0, false), (1, true), (BasketLineRequest.MaximumQuantity, true), (BasketLineRequest.MaximumQuantity + 1, false) })
    {
        var line = new BasketLineRequest { ProductId = 1, Quantity = quantity };
        var context = new ValidationContext(line) { MemberName = nameof(BasketLineRequest.Quantity) };
        if (Validator.TryValidateProperty(quantity, context, new List<ValidationResult>()) != expected)
        {
            throw new InvalidOperationException($"Unexpected validation result for quantity {quantity}.");
        }
    }
}

static void ProductPriceBoundsAreCultureIndependent()
{
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        foreach (var cultureName in new[] { "tr-TR", "en-US", "de-DE" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var (price, expected) in new[] { (0m, false), (0.009m, false), (0.01m, true), (100.50m, true), (999999999m, true), (999999999.01m, false) })
            {
                var product = new ProductSaveDto { Price = price };
                var context = new ValidationContext(product) { MemberName = nameof(ProductSaveDto.Price) };
                var valid = Validator.TryValidateProperty(price, context, new List<ValidationResult>());
                if (valid != expected)
                {
                    throw new InvalidOperationException($"Unexpected price validation for {price} under {cultureName}.");
                }
            }
        }
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
    }
}

static void SatirEkleInitializesOrderLines()
{
    var order = new Order();

    order.SatirEkle(new Product
    {
        Id = 1,
        Definition = "Ders urunu",
        Price = 100m,
        VatRate = 20
    }, 1);

    if (order.OrderLines.Count != 1)
    {
        throw new InvalidOperationException($"Expected 1 line, got {order.OrderLines.Count}.");
    }
}

static void SatirEkleSnapshotsVatInclusiveUnitPrice()
{
    var order = new Order();

    order.SatirEkle(new Product
    {
        Id = 1,
        Definition = "Ders urunu",
        Price = 100m,
        VatRate = 20
    }, 2);

    var line = order.OrderLines.Single();
    if (line.UnitPrice != 120m || line.LineTotalAmount != 240m || order.TotalAmount != 240m)
    {
        throw new InvalidOperationException($"Expected unit 120.00 and total 240.00, got unit {line.UnitPrice:0.00} and total {order.TotalAmount:0.00}.");
    }
}

static void CalculateTotalKeepsStoredSnapshotWhenProductPriceChanges()
{
    var order = new Order
    {
        OrderLines = new List<OrderLine>
        {
            new()
            {
                ProductId = 1,
                Product = new Product
                {
                    Id = 1,
                    Definition = "Ders urunu",
                    Price = 120m,
                    VatRate = 20
                },
                UnitPrice = 120m,
                Quantity = 2
            }
        }
    };

    order.CalculateTotal();

    if (order.TotalAmount != 240m)
    {
        throw new InvalidOperationException($"Expected total 240.00, got {order.TotalAmount:0.00}.");
    }
}
