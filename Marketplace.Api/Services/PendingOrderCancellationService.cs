using Marketplace.Api.Models;
using Marketplace.Api.Repositories;

namespace Marketplace.Api.Services;

public class PendingOrderCancellationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisCacheService _cache;
    private readonly ILogger<PendingOrderCancellationService> _logger;

    public PendingOrderCancellationService(
        IServiceScopeFactory scopeFactory, RedisCacheService  cache, ILogger<PendingOrderCancellationService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var orderRepository =
                    scope.ServiceProvider
                        .GetRequiredService<OrderRepository>();

                var orderIds =
                    await orderRepository
                        .GetExpiredPendingOrderIdsAsync(stoppingToken);

                foreach (var orderId in orderIds)
                {
                    var userId =
                        await orderRepository
                            .CancelExpiredPendingOrderAsync(orderId, stoppingToken);

                    if (userId.HasValue)
                    {
                        var cacheKey = new OrderCacheKey(userId.Value,  orderId);
                        
                        await _cache.RemoveAsync(cacheKey.ToString());
                        
                        _logger.LogInformation("Order {OrderId} was automatically cancelled.", orderId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending order cancellation error.");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(1),
                stoppingToken);
        }
    }
}