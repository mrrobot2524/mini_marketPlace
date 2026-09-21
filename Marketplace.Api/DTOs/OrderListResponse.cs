namespace Marketplace.Api.DTOs;

public record OrderListResponse
{
    public List<OrderResponse> Items { get; init; } = new();
    
    public int Page  { get; init; }
    
    public int PageSize { get; init; }
    
    public int TotalCount { get; init; }
    
    public int TotalPages { get; init; }
}