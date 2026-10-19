using System.Security.Claims;
using HomeLiveStore.Application.DTOs;
using HomeLiveStore.Application.Exceptions;
using HomeLiveStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Api.Controllers
{
  [ApiController]
  [Authorize]
  public sealed class OrdersController(IOrderService orderService) : ControllerBase
  {
    [HttpPost("api/orders")]
    public async Task<ActionResult<OrderResponse>> Create(OrderCreateRequest request, CancellationToken cancellationToken)
    {
      var order = await orderService.CreateOrderAsync(CurrentUserId(), request, cancellationToken);
      return Created($"/api/orders/{order.Id}", order);
    }

    [HttpGet("api/orders")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> List(CancellationToken cancellationToken)
    {
      return Ok(await orderService.GetOrdersAsync(CurrentUserId(), false, cancellationToken));
    }

    [HttpGet("api/orders/{id:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<OrderResponse>> Detail(int id, CancellationToken cancellationToken)
    {
      return Ok(await orderService.GetOrderAsync(id, CurrentUserId(), IsManager(), cancellationToken));
    }

    [HttpPost("api/orders/{id:int}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(int id, CancellationToken cancellationToken)
    {
      return Ok(await orderService.CancelOrderAsync(id, CurrentUserId(), IsManager(), cancellationToken));
    }

    [HttpGet("api/admin/orders")]
    [Authorize(Roles = "Manager")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> AdminList(CancellationToken cancellationToken)
    {
      return Ok(await orderService.GetOrdersAsync(CurrentUserId(), true, cancellationToken));
    }

    [HttpPut("api/admin/orders/{id:int}/status")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
      return Ok(await orderService.UpdateStatusAsync(id, request.Status, cancellationToken));
    }

    private int CurrentUserId()
    {
      var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
      return int.TryParse(value, out var id) ? id : throw new StoreValidationException("Oturum bulunamadi.");
    }

    private bool IsManager() => User.IsInRole("Manager");
  }
}
