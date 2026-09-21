namespace Marketplace.Api.Models;

public readonly struct OrderCacheKey
{
    private readonly int _userId;
    private readonly int _orderId;
    
    public OrderCacheKey(int userId, int orderId)
    {
        _userId = userId;
        _orderId = orderId;
    }

    public override string ToString() => $"order:{_userId}:{_orderId}";
}