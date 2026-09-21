using System.Security.Claims;
using Marketplace.Api.DTOs;
using Marketplace.Api.Exceptions;
using Marketplace.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;
[Authorize]
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    private int? GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        
        return userId;
    }
    
   

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] CreateOrderRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new
            {
                message = "Idempotency-Key header is required."
            });
        }

        if (idempotencyKey.Length > 255)
        {
            return BadRequest(new
            {
                message = "Idempotency-Key must not exceed 255 characters."
            });
        }

        try
        {
            var orderId = await _orderService.CreateOrderAsync(
                userId.Value,
                idempotencyKey,
                request,
                HttpContext.RequestAborted);

            return Ok(new
            {
                id = orderId
            });
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }
    
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default
        )
    {
        if (page < 1)
        {
            return BadRequest(new
            {
                message = "Page must be greater than or equal to 1."
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                message = "PageSize must be between 1 and 100."
            });
        }
        
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var orders = await _orderService.GetOrdersAsync(userId.Value, page, pageSize, cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var order = await _orderService.GetOrderByIdAsync(id, userId.Value, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        return Ok(order);
    }
    
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            await _orderService.CancelOrderAsync(id, userId.Value, HttpContext.RequestAborted);
            return Ok(new
            {
                message = "Order cancelled successfully"
            });
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
        catch (OrderCannotBeCancelledException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}