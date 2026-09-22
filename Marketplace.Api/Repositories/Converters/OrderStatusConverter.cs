using Marketplace.Api.Models;

namespace Marketplace.Api.Repositories.Converters;

public static class OrderStatusConverter
{
    public static OrderStatus FromDbString(string value)
    {
        return value switch
        {
            "pending" => OrderStatus.Pending,
            "confirmed" => OrderStatus.Confirmed,
            "cancelled" => OrderStatus.Cancelled,
            _ => throw new InvalidOperationException(
                $"Unknown order status: '{value}'")
        };
    }

    public static string ToDbString(this OrderStatus value)
    {
        return value switch
        {
            OrderStatus.Pending => "pending",
            OrderStatus.Confirmed => "confirmed",
            OrderStatus.Cancelled => "cancelled",
            _ => throw new InvalidOperationException(
                $"Unknown order status: {value}")
        };
    }
}