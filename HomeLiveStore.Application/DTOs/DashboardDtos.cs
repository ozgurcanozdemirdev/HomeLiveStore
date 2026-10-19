namespace HomeLiveStore.Application.DTOs
{
  /// <summary>Admin panel ozet cevabi.</summary>
  public sealed record DashboardResponse(
    int ProductCount,
    int CategoryCount,
    int CustomerCount,
    int OrderCount,
    int PendingOrderCount,
    decimal TotalRevenue,
    IReadOnlyList<ProductListDto> LowStockProducts,
    IReadOnlyList<OrderResponse> RecentOrders);
}
