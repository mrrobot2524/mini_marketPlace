namespace Marketplace.Api.Exceptions;

public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId) : base($"Order {orderId} not found.")
    {
        
    }
}