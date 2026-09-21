namespace Marketplace.Api.Models;

public record OrderItem(int Id, int OrderId, int ProductId, int Quantity, decimal Price);