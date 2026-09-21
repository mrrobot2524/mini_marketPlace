namespace Marketplace.Api.Models;

public record Product(int Id, string Name, decimal Price, int StockQuantity);