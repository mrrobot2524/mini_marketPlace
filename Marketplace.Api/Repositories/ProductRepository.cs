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
}