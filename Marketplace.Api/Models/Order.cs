namespace Marketplace.Api.Models;

public record Order(int Id, int UserId, string Status, DateTime CreatedAt);