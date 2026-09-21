using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.DTOs;

public class CreateOrderRequest
{
   [Required]
   [MinLength(1)]
   public List<CreateOrderItemRequest> Items { get; set; } = new();
}

