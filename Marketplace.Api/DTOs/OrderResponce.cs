namespace Marketplace.Api.DTOs;

public record OrderResponse
{
    public int Id { get; init; }

    public int UserId { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public List<OrderItemResponse> Items { get; } = new();
}

public record OrderItemResponse
{
    public int ProductId { get; init; }

    public int Quantity { get; init; }

    public decimal Price { get; init; }
}