using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.DTOs;

public record CreateProductRequest
{
    [Required] 
    public string Name { get; init; } = string.Empty;
    public decimal Price {get; init;}
    public int StockQuantity {get; init;}
}