namespace Marketplace.Api.Exceptions;

public class OrderCannotBeCancelledException : Exception
{
    public OrderCannotBeCancelledException(int orderId, string status) : base($"Order {orderId} can't be cancelled because its status is '{status}'. ")
    {
        
    }
}