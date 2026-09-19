namespace Marketplace.Api.DTOs;

public class CreateOrderRequest
{
   public List<CreateOrderItemRequest> Items { get; set; }
}

public class CreateOrderItemRequest
{
   public int ProductId { get; set; }
   public int Quantity { get; set; }
}