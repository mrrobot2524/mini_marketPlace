using Npgsql;

namespace Marketplace.Api.Repositories;

public class IdempotencyKeyRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public IdempotencyKeyRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }
    
    public async Task<int?> FindByUserAndKeyAsync(
        DbSession session,
        int userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT order_id
                           FROM idempotency_keys
                           WHERE user_id = $1
                             AND idempotency_key = $2;
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction);

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(idempotencyKey);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null ? null : (int)result;
    }

    public async Task<int?> FindByUserAndKeyAsync(int userId, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var session = new DbSession(connection, Transaction: null);
        
        return await FindByUserAndKeyAsync(session, userId, idempotencyKey, cancellationToken);
    }

    public async Task SaveAsync(DbSession session, int userId, string idempotencyKey, int orderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO idempotency_keys (user_id, idempotency_key, order_id)
                           VALUES ($1, $2, $3);
                           """;

        await using var command = new NpgsqlCommand(
            sql,
            session.Connection,
            session.Transaction
        );

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(idempotencyKey);
        command.Parameters.AddWithValue(orderId);
    }
    
}