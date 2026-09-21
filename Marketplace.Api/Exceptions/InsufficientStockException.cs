namespace Marketplace.Api.Exceptions;

public class InsufficientStockException : Exception
{
    public InsufficientStockException(int productId)
        : base($"Not enough stock for product {productId}.")
    {
    }
}