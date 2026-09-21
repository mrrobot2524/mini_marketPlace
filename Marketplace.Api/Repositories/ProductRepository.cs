using Marketplace.Api.Models;
using Npgsql;

namespace Marketplace.Api.Repositories;

public class ProductRepository
{
    private readonly NpgsqlDataSource _dataSource;
    public ProductRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        const string sql = """
                           INSERT INTO products (name, price, stock_quantity)
                           VALUES ($1, $2, $3)
                           RETURNING id, name, price,stock_quantity;
                           """;

        await using var comand = _dataSource.CreateCommand(sql);

        comand.Parameters.AddWithValue((product.Name));
        comand.Parameters.AddWithValue((product.Price));
        comand.Parameters.AddWithValue((product.StockQuantity));
        
        await using var reader = await comand.ExecuteReaderAsync();

        await reader.ReadAsync();

        return new Product
        (
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3)
        );
    }
    
    public async Task<Product?> GetForUpdateAsync(
        DbSession session,
        int productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, name, price, stock_quantity
                           FROM products
                           WHERE id = $1
                           FOR UPDATE;
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction);

        command.Parameters.AddWithValue(productId);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Product(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3));
    }
    
    public async Task UpdateStockAsync(
        DbSession session,
        int productId,
        int newStockQuantity,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE products
                           SET stock_quantity = $1
                           WHERE id = $2;
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction);

        command.Parameters.AddWithValue(newStockQuantity);
        command.Parameters.AddWithValue(productId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}