using System.ComponentModel.DataAnnotations;
namespace Marketplace.Api.DTOs;

public record UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;
    
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; init; }
    
    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }
}