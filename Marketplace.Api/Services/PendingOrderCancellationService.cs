using Marketplace.Api.Repositories;

namespace Marketplace.Api.Services;

public class PendingOrderCancellationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    private readonly RedisCacheService _cache;

    public PendingOrderCancellationService(
        IServiceScopeFactory scopeFactory, RedisCacheService  cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
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
                        var cacheKey = $"order:{userId.Value}:{orderId}";
                        
                        await _cache.RemoveAsync(cacheKey);
                        
                        Console.WriteLine(
                            $"Order {orderId} was automatically cancelled.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Pending order cancellation error: {ex.Message}");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(1),
                stoppingToken);
        }
    }
}