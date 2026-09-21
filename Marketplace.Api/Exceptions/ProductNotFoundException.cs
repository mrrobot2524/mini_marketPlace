namespace Marketplace.Api.Exceptions;

public class ProductNotFoundException : Exception
{
    public ProductNotFoundException(int productId)
        : base($"Product {productId} not found.")
    {
    }
}