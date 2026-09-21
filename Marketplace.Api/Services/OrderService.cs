using System.Text.Json;
using Marketplace.Api.DTOs;
using Marketplace.Api.Models;
using Marketplace.Api.Repositories;

namespace Marketplace.Api.Services;

public class OrderService
{
    private readonly OrderRepository _orderRepository;
    private readonly RedisCacheService _cache;

    public OrderService(OrderRepository orderRepository, RedisCacheService cache)
    {
        _orderRepository = orderRepository;
        _cache = cache;
    }

    public async Task<int> CreateOrderAsync(
        int userId,
        string idempotencyKey,
        CreateOrderRequest request,
        CancellationToken cancellationToken
        )
    {
        return await _orderRepository.CreateOrderAsync(
            userId,
            idempotencyKey,
            request.Items, 
            cancellationToken);
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(
        int orderId,
        int userId,
        CancellationToken cancellationToken)
    {
        var cacheKey = new OrderCacheKey(userId, orderId);

        var cachedOrder = await _cache.GetAsync(cacheKey.ToString());

        if (cachedOrder is not null)
        {
            return JsonSerializer.Deserialize<OrderResponse>(
                cachedOrder);
        }

        var order =
            await _orderRepository.GetOrderByIdAsync(
                orderId,
                userId,
                cancellationToken);

        if (order is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(order);

        await _cache.SetAsync(
            cacheKey.ToString(),
            json,
            TimeSpan.FromMinutes(5));

        return order;
    }

    public async Task<OrderListResponse> GetOrdersAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken
        )
    {
        var orders = await _orderRepository.GetOrdersAsync(
            userId,
            page,
            pageSize,
            cancellationToken
            );

        var totalCount = await _orderRepository.GetOrdersCountAsync(userId, cancellationToken);

        var totalPages = (int)Math.Ceiling(
            (double)totalCount / pageSize);

        return new OrderListResponse
        {
            Items = orders,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task CancelOrderAsync(
        int orderId,
        int userId,
        CancellationToken cancellationToken)
    {
        await _orderRepository.CancelOrderAsync(
            orderId,
            userId,
            cancellationToken);

        var cacheKey = new OrderCacheKey(userId, orderId);

        await _cache.RemoveAsync(cacheKey.ToString());
    }
}