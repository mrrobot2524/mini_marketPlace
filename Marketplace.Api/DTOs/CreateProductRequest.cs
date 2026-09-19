namespace Marketplace.Api.DTOs;

public class CreateProductRequest
{
    public string Name {get; set;}
    public decimal Price {get; set;}
    public int StockQuantity {get; set;}
}