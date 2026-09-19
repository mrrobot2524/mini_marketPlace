namespace Marketplace.Api.Models;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}