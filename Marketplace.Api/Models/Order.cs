namespace Marketplace.Api.Models;

public record Order(int Id, int UserId, OrderStatus Status, DateTime CreatedAt);