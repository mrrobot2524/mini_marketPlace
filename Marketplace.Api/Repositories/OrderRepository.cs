using Marketplace.Api.DTOs;
using Marketplace.Api.Exceptions;
using Marketplace.Api.Models;
using Npgsql;

namespace Marketplace.Api.Repositories;

public class OrderRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly OrderItemRepository _orderItemRepository;
    private readonly ProductRepository _productRepository;
    private const int PendingOrderTimeoutMinutes = 15;

    public OrderRepository(NpgsqlDataSource dataSource, IdempotencyKeyRepository idempotencyKeyRepository, OrderItemRepository orderItemRepository, ProductRepository productRepository)
    {
        _dataSource = dataSource;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _orderItemRepository = orderItemRepository;
        _productRepository = productRepository;
    }
    

    public async Task<int> InsertAsync(DbSession session, int userId, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO orders (user_id, status)
                           VALUES ($1, 'pending')
                           RETURNING id;
                           """;

        await using var command = new NpgsqlCommand(sql, session.Connection, session.Transaction);

        command.Parameters.AddWithValue(userId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return (int)result!;
    }

    public async Task UpdateStatusAsync(DbSession session, int orderId, string newStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
                               UPDATE orders
                               SET status = $1
                               WHERE id = $2;
                           """;
        
        await using var command = new NpgsqlCommand(sql, session.Connection, session.Transaction);

        command.Parameters.AddWithValue(newStatus);
        command.Parameters.AddWithValue(orderId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    public async Task<int> CreateOrderAsync(
        int userId,
        string idempotencyKey,
        List<CreateOrderItemRequest> items,
        CancellationToken cancellationToken
        )
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var session = new DbSession(connection, transaction);

        try
        {
            // Проверяем Idempotency-Key
            var existingOrderId =
                await _idempotencyKeyRepository.FindByUserAndKeyAsync(session, userId, idempotencyKey,
                    cancellationToken);

            if (existingOrderId.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);

                return existingOrderId.Value;
            }

            // Создаём заказ
            var orderId = await InsertAsync(session, userId, cancellationToken);

            var quantitiesByProduct = new Dictionary<int, int>();

            foreach (var item in items)
            {
                if (quantitiesByProduct.TryGetValue(item.ProductId, out var existingQuantity))
                {
                    quantitiesByProduct[item.ProductId] = existingQuantity + item.Quantity;
                }
                else
                {
                    quantitiesByProduct[item.ProductId] = item.Quantity;
                }
            }

            var mergedItems = quantitiesByProduct.Select(pair => new CreateOrderItemRequest
            {
                ProductId = pair.Key,
                Quantity = pair.Value
            }).OrderBy(x => x.ProductId).ToList();

            foreach (var item in mergedItems)
            {
                var product = await _productRepository.GetForUpdateAsync(session, item.ProductId, cancellationToken);

                if (product is null)
                {
                    throw new ProductNotFoundException(item.ProductId);
                }

                if (product.StockQuantity < item.Quantity)
                {
                    throw new InsufficientStockException(item.ProductId);
                }

                var newStockQuantity = product.StockQuantity - item.Quantity;
                
                await _productRepository.UpdateStockAsync(session, product.Id, newStockQuantity, cancellationToken);

                await _orderItemRepository.CreateAsync(session, orderId, product.Id, item.Quantity, product.Price,
                    cancellationToken);
            }
            
            await _idempotencyKeyRepository.SaveAsync(session, userId, idempotencyKey, orderId,  cancellationToken);
            
            await transaction.CommitAsync(cancellationToken);
            
            return orderId;
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);

            var existingOrderId =
                await _idempotencyKeyRepository.FindByUserAndKeyAsync(userId, idempotencyKey,
                    cancellationToken);

            if (existingOrderId.HasValue)
            {
                return existingOrderId.Value;
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    public async Task<OrderResponse?> GetOrderByIdAsync(int orderId, int userId, CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT
                               o.id,
                               o.user_id,
                               o.status,
                               o.created_at,
                               oi.product_id,
                               oi.quantity,
                               oi.price
                           FROM orders o
                           LEFT JOIN order_items oi
                               ON oi.order_id = o.id
                           WHERE o.id = $1 AND o.user_id = $2
                           ORDER BY oi.id;
                           """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(orderId);
        command.Parameters.AddWithValue(userId);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        OrderResponse? order = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            order ??= ReadOrder(reader);

            var item = ReadOrderItem(reader);

            if (item is not null)
            {
                order.Items.Add(item);
            }
        }

        return order;
    }
    
    public async Task<List<OrderResponse>> GetOrdersAsync(int userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT
                               o.id,
                               o.user_id,
                               o.status,
                               o.created_at,
                               oi.product_id,
                               oi.quantity,
                               oi.price
                           FROM (
                               SELECT id, user_id, status, created_at
                               FROM orders
                               WHERE user_id = $1
                               ORDER BY created_at DESC
                               LIMIT $2
                               OFFSET $3
                           ) o 
                           LEFT JOIN order_items oi
                           ON oi.order_id = o.id
                           ORDER BY o.created_at DESC, oi.id;
                           """;

        var offset = (page -1) * pageSize;
        
        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(pageSize);
        command.Parameters.AddWithValue(offset);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var orders = new List<OrderResponse>();

        OrderResponse? currentOrder = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = reader.GetInt32(0);

            if (currentOrder is null || currentOrder.Id != orderId)
            {
                currentOrder = ReadOrder(reader);
                orders.Add(currentOrder);
            }

            var item = ReadOrderItem(reader);
            
            if (item is not null)
            {
                currentOrder.Items.Add(item);
            }
        }

        return orders;
    }
    
    public async Task<int> GetOrdersCountAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT COUNT(*)
                           FROM orders
                           WHERE user_id = $1;
                           """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(userId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result);
    }
    
    public async Task CancelOrderAsync(
        int orderId,
        int userId,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionAsync(async (session, ct) =>
        {
            const string lockSql = """
                                   SELECT status
                                   FROM orders
                                   WHERE id = $1
                                     AND user_id = $2
                                   FOR UPDATE;
                                   """;

            await using var lockCommand = new NpgsqlCommand(
                lockSql, session.Connection, session.Transaction);

            lockCommand.Parameters.AddWithValue(orderId);
            lockCommand.Parameters.AddWithValue(userId);

            var status = (string?)await lockCommand.ExecuteScalarAsync(ct);

            if (status is null)
            {
                throw new OrderNotFoundException(orderId);
            }

            if (status == "cancelled")
            {
                return;   // ← idempotent: уже отменён, выходим без значения
            }

            if (status != "pending")
            {
                throw new OrderCannotBeCancelledException(orderId, status);
            }

            var items = await _orderItemRepository
                .GetByOrderIdForUpdateAsync(session, orderId, ct);

            foreach (var item in items)
            {
                var product = await _productRepository
                    .GetForUpdateAsync(session, item.ProductId, ct);

                if (product is null)
                {
                    throw new ProductNotFoundException(item.ProductId);
                }

                var newStock = product.StockQuantity + item.Quantity;

                await _productRepository.UpdateStockAsync(
                    session, product.Id, newStock, ct);
            }

            await UpdateStatusAsync(session, orderId, "cancelled", ct);
        
            // ← БЕЗ return userId; — лямбда просто завершается
        }, cancellationToken);
    }
    
    public async Task<List<int>> GetExpiredPendingOrderIdsAsync(CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT id
                           FROM orders
                           WHERE status = 'pending'
                             AND created_at <= NOW() - make_interval(mins => $1)
                           ORDER BY id;
                           """;

        await using var command =
            new NpgsqlCommand(sql, connection);
        
        command.Parameters.AddWithValue(PendingOrderTimeoutMinutes);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var orderIds = new List<int>();

        while (await reader.ReadAsync(cancellationToken))
        {
            orderIds.Add(reader.GetInt32(0));
        }

        return orderIds;
    }
    
    public async Task<int?> CancelExpiredPendingOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync<int?>(async (session, ct) =>
        {
            const string lockSql = """
                                   SELECT user_id, status, created_at
                                   FROM orders
                                   WHERE id = $1
                                   FOR UPDATE;
                                   """;

            await using var lockCommand = new NpgsqlCommand(
                lockSql, session.Connection, session.Transaction);

            lockCommand.Parameters.AddWithValue(orderId);

            await using var reader = await lockCommand.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            var userId = reader.GetInt32(0);
            var status = reader.GetString(1);
            var createdAt = reader.GetFieldValue<DateTime>(2);

            await reader.CloseAsync();

            if (status != "pending")
            {
                return null;
            }

            var timeoutThreshold = DateTime.UtcNow.AddMinutes(-PendingOrderTimeoutMinutes);

            if (createdAt > timeoutThreshold)
            {
                return null;
            }

            var items = await _orderItemRepository
                .GetByOrderIdForUpdateAsync(session, orderId, ct);

            foreach (var item in items)
            {
                var product = await _productRepository
                    .GetForUpdateAsync(session, item.ProductId, ct);

                if (product is null)
                {
                    throw new ProductNotFoundException(item.ProductId);
                }

                var newStock = product.StockQuantity + item.Quantity;

                await _productRepository.UpdateStockAsync(
                    session, product.Id, newStock, ct);
            }

            await UpdateStatusAsync(session, orderId, "cancelled", ct);

            return userId;
        }, cancellationToken);
    }
    
    
    private static OrderResponse ReadOrder(NpgsqlDataReader reader)
    {
        return new OrderResponse
        {
            Id = reader.GetInt32(0),
            UserId = reader.GetInt32(1),
            Status = reader.GetString(2),
            CreatedAt = reader.GetDateTime(3)
        };
    }

    private static OrderItemResponse? ReadOrderItem(NpgsqlDataReader reader)
    {
        if (reader.IsDBNull(4))
        {
            return null;
        }

        return new OrderItemResponse
        {
            ProductId = reader.GetInt32(4),
            Quantity = reader.GetInt32(5),
            Price = reader.GetDecimal(6)
        };
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