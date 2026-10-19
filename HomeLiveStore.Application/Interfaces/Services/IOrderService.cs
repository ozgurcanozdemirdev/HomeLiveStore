using HomeLiveStore.Application.DTOs;
using HomeLiveStore.Domain.Enums;

namespace HomeLiveStore.Application.Interfaces.Services
{
  public interface IOrderService
  {
    Task<OrderResponse> CreateOrderAsync(int userId, OrderCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResponse>> GetOrdersAsync(int userId, bool includeAll, CancellationToken cancellationToken = default);
    Task<OrderResponse> GetOrderAsync(int orderId, int userId, bool includeAll, CancellationToken cancellationToken = default);
    Task<OrderResponse> CancelOrderAsync(int orderId, int userId, bool isManager, CancellationToken cancellationToken = default);
    Task<OrderResponse> UpdateStatusAsync(int orderId, OrderStatus status, CancellationToken cancellationToken = default);
  }
}
