using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.DTOs;

public record CreateOrderRequest
{
   [Required]
   [MinLength(1)]
   public List<CreateOrderItemRequest> Items { get; init; } = new();
}

