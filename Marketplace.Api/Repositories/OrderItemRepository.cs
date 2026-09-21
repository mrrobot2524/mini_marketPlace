using Marketplace.Api.Models;
using Npgsql;

namespace Marketplace.Api.Repositories;

public class OrderItemRepository
{
    private readonly NpgsqlDataSource _dataSource;
    
    public OrderItemRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task CreateAsync(
        DbSession session,
        int orderId,
        int productId,
        int quantity,
        decimal price,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
                            INSERT INTO order_items (order_id, product_id, quantity, price) VALUES ($1, $2, $3, $4);
                           """;
        
        await using var command = new NpgsqlCommand(sql, session.Connection, session.Transaction);
        
        command.Parameters.AddWithValue(orderId);
        command.Parameters.AddWithValue(productId);
        command.Parameters.AddWithValue(quantity);
        command.Parameters.AddWithValue(price);
        
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    public async Task<List<OrderItemLine>> GetByOrderIdAsync(
        DbSession session,
        int orderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT product_id, quantity
                           FROM order_items
                           WHERE order_id = $1
                           ORDER BY product_id;
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction);

        command.Parameters.AddWithValue(orderId);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<OrderItemLine>();

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new OrderItemLine(
                reader.GetInt32(0),
                reader.GetInt32(1)));
        }

        return items;
    }

    public async Task<List<OrderItemLine>> GetByOrderIdForUpdateAsync(
        DbSession session,
        int orderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT product_id, quantity
                           FROM order_items
                           WHERE order_id = $1
                           ORDER BY product_id
                           FOR UPDATE;
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction);

        command.Parameters.AddWithValue(orderId);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<OrderItemLine>();

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new OrderItemLine(
                reader.GetInt32(0),
                reader.GetInt32(1)));
        }

        return items;
    }
}