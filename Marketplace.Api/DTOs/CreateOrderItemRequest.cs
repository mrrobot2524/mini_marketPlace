using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.DTOs;

public record CreateOrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }
    
    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}