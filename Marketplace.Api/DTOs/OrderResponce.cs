namespace Marketplace.Api.DTOs;

public class OrderResponse
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<OrderItemResponse> Items { get; set; } = new();
}

public class OrderItemResponse
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}