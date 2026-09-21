using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.DTOs;

public class CreateProductRequest
{
    [Required] 
    public string Name { get; set; } = string.Empty;
    public decimal Price {get; set;}
    public int StockQuantity {get; set;}
}