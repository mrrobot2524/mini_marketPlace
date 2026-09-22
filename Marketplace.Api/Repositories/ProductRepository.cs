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

    public async Task<Product> CreateAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO products (name, price, stock_quantity)
                           VALUES ($1, $2, $3)
                           RETURNING id, name, price, stock_quantity;
                           """;

        await using var command = _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue(product.Name);
        command.Parameters.AddWithValue(product.Price);
        command.Parameters.AddWithValue(product.StockQuantity);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);

        return new Product(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3));
    }

    public async Task<Product?> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT id, name, price, stock_quantity
                           FROM products
                           WHERE id = $1;
                           """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(productId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

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

    public async Task<List<Product>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT id, name, price, stock_quantity
                           FROM products
                           ORDER BY id
                           LIMIT $1 OFFSET $2;
                           """;

        var offset = (page - 1) * pageSize;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(pageSize);
        command.Parameters.AddWithValue(offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var products = new List<Product>();

        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new Product(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetInt32(3)));
        }

        return products;
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT COUNT(*)
                           FROM products;
                           """;

        await using var command = new NpgsqlCommand(sql, connection);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result);
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

    public async Task<bool> UpdateAsync(
        int productId,
        string name,
        decimal price,
        int stockQuantity,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync<bool>(async (session, ct) =>
        {
            return await UpdateAsync(
                session,
                productId,
                name,
                price,
                stockQuantity,
                ct);
        }, cancellationToken);
    }

    private async Task<bool> UpdateAsync(
        DbSession session,
        int productId,
        string name,
        decimal price,
        int stockQuantity,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE products
                           SET name = $1,
                               price = $2,
                               stock_quantity = $3
                           WHERE id = $4;
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction);

        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue(price);
        command.Parameters.AddWithValue(stockQuantity);
        command.Parameters.AddWithValue(productId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync<bool>(async (session, ct) =>
        {
            return await DeleteAsync(session, productId, ct);
        }, cancellationToken);
    }

    private async Task<bool> DeleteAsync(
        DbSession session,
        int productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           DELETE FROM products
                           WHERE id = $1;
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction);

        command.Parameters.AddWithValue(productId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        return rowsAffected > 0;
    }

    private async Task ExecuteInTransactionAsync(
        Func<DbSession, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var session = new DbSession(connection, transaction);

        try
        {
            await action(session, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<T> ExecuteInTransactionAsync<T>(
        Func<DbSession, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var session = new DbSession(connection, transaction);

        try
        {
            var result = await action(session, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}