using Marketplace.Api.DTOs;
using Marketplace.Api.Exceptions;
using Marketplace.Api.Models;
using Npgsql;

namespace Marketplace.Api.Repositories;

public class OrderRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public OrderRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int?> FindExistingOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int userId,
        string idempotencyKey)
    {
        const string sql = """
                           SELECT order_id
                           FROM idempotency_keys
                           WHERE user_id = $1
                             AND idempotency_key = $2;
                           """;

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(idempotencyKey);

        var result = await command.ExecuteScalarAsync();

        if (result is null)
        {
            return null;
        }

        return (int)result;
    }
    
    public async Task<int?> FindExistingOrderAsync(
        int userId,
        string idempotencyKey)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync();

        const string sql = """
                           SELECT order_id
                           FROM idempotency_keys
                           WHERE user_id = $1
                             AND idempotency_key = $2;
                           """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(idempotencyKey);

        var result = await command.ExecuteScalarAsync();

        if (result is null)
        {
            return null;
        }

        return (int)result;
    }

    public async Task<Product?> GetProductForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int productId)
    {
        const string sql = """
                           SELECT id, name, price, stock_quantity
                           FROM products
                           WHERE id = $1
                           FOR UPDATE;
                           """;

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue(productId);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Product
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Price = reader.GetDecimal(2),
            StockQuantity = reader.GetInt32(3)
        };
    }

    public async Task UpdateStockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int productId,
        int newStockQuantity)
    {
        const string sql = """
                                UPDATE products 
                                SET stock_quantity = $1  
                                WHERE id = $2;
                           """;
        
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(newStockQuantity);
        command.Parameters.AddWithValue(productId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> InsertOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int userId)
    {
        const string sql = """
                           INSERT INTO orders (user_id, status)
                           VALUES ($1, 'pending')
                           RETURNING id;
                           """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue(userId);

        var result = await command.ExecuteScalarAsync();

        return (int)result!;
    }
    
    public async Task CreateOrderItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int orderId,
        int productId,
        int quantity,
        decimal price)
    {
        const string sql = """
                           INSERT INTO order_items
                               (order_id, product_id, quantity, price)
                           VALUES
                               ($1, $2, $3, $4);
                           """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue(orderId);
        command.Parameters.AddWithValue(productId);
        command.Parameters.AddWithValue(quantity);
        command.Parameters.AddWithValue(price);

        await command.ExecuteNonQueryAsync();
    }
    
    public async Task SaveIdempotencyKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int userId,
        string idempotencyKey,
        int orderId)
    {
        const string sql = """
                           INSERT INTO idempotency_keys
                               (user_id, idempotency_key, order_id)
                           VALUES
                               ($1, $2, $3);
                           """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(idempotencyKey);
        command.Parameters.AddWithValue(orderId);

        await command.ExecuteNonQueryAsync();
    }
    
    
    public async Task<int> CreateOrderAsync(
        int userId,
        string idempotencyKey,
        List<CreateOrderItemRequest> items)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            // 1. Проверяем Idempotency-Key
            var existingOrderId = await FindExistingOrderAsync(
                connection,
                transaction,
                userId,
                idempotencyKey);

            if (existingOrderId.HasValue)
            {
                await transaction.CommitAsync();

                return existingOrderId.Value;
            }

            // 2. Создаём заказ
            var orderId = await InsertOrderAsync(
                connection,
                transaction,
                userId);

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

            var mergedItems = new List<CreateOrderItemRequest>();

            foreach (var pair in quantitiesByProduct)
            {
                mergedItems.Add(new CreateOrderItemRequest
                {
                    ProductId = pair.Key,
                    Quantity = pair.Value
                });
            }
            
            mergedItems.Sort((a,b) => a.ProductId.CompareTo(b.ProductId));
            
            foreach (var item in mergedItems)
            {
                var product = await GetProductForUpdateAsync(
                    connection,
                    transaction,
                    item.ProductId);

                if (product is null)
                {
                    throw new ProductNotFoundException(item.ProductId);
                }

                if (product.StockQuantity < item.Quantity)
                {
                    throw new InsufficientStockException(item.ProductId);
                }

                var newStockQuantity =
                    product.StockQuantity - item.Quantity;

                await UpdateStockAsync(
                    connection,
                    transaction,
                    product.Id,
                    newStockQuantity);

                await CreateOrderItemAsync(
                    connection,
                    transaction,
                    orderId,
                    product.Id,
                    item.Quantity,
                    product.Price);
            }
            
            await SaveIdempotencyKeyAsync(
                connection,
                transaction,
                userId,
                idempotencyKey,
                orderId);

            await transaction.CommitAsync();

            return orderId;
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync();

            var existingOrderId = await FindExistingOrderAsync(
                userId,
                idempotencyKey);

            if (existingOrderId.HasValue)
            {
                return existingOrderId.Value;
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync();
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
            if (order is null)
            {
                order = new OrderResponse
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Status = reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3)
                };
            }

            if (!reader.IsDBNull(4))
            {
                order.Items.Add(new OrderItemResponse
                {
                    ProductId = reader.GetInt32(4),
                    Quantity = reader.GetInt32(5),
                    Price = reader.GetDecimal(6)
                });
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
                           ORDER BY o.created_at DESC, oi.id
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
                currentOrder = new OrderResponse
                {
                    Id = orderId,
                    UserId = reader.GetInt32(1),
                    Status = reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3)
                };

                orders.Add(currentOrder);
            }

            if (!reader.IsDBNull(4))
            {
                currentOrder.Items.Add(new OrderItemResponse
                {
                    ProductId = reader.GetInt32(4),
                    Quantity = reader.GetInt32(5),
                    Price = reader.GetDecimal(6)
                });
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
    
    public async Task CancelOrderAsync(int orderId, int userId)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            // 1. Получаем заказ и блокируем его
            const string orderSql = """
                                    SELECT status
                                    FROM orders
                                    WHERE id = $1
                                      AND user_id = $2
                                    FOR UPDATE;
                                    """;

            await using var orderCommand =
                new NpgsqlCommand(
                    orderSql,
                    connection,
                    transaction);

            orderCommand.Parameters.AddWithValue(orderId);
            orderCommand.Parameters.AddWithValue(userId);

            var result =
                await orderCommand.ExecuteScalarAsync();

            if (result is null)
            {
                throw new OrderNotFoundException(orderId);
            }

            var status = (string)result;

            // 2. Если заказ уже cancelled —
            // ничего больше не делаем
            if (status == "cancelled")
            {
                await transaction.CommitAsync();
                return;
            }

            // 3. Отменять можно только pending
            if (status != "pending")
            {
                throw new OrderCannotBeCancelledException(orderId, status);
            }

            const string itemsSql = """
                                    SELECT product_id, quantity
                                    FROM order_items
                                    WHERE order_id = $1
                                    ORDER BY product_id;
                                    """;

            await using var itemsCommand =
                new NpgsqlCommand(
                    itemsSql,
                    connection,
                    transaction);

            itemsCommand.Parameters.AddWithValue(orderId);

            await using var reader =
                await itemsCommand.ExecuteReaderAsync();

            var items = new List<(int ProductId, int Quantity)>();

            while (await reader.ReadAsync())
            {
                items.Add((
                    reader.GetInt32(0),
                    reader.GetInt32(1)
                ));
            }

            await reader.DisposeAsync();
            
            foreach (var item in items)
            {
                const string productSql = """
                                          SELECT stock_quantity
                                          FROM products
                                          WHERE id = $1
                                          FOR UPDATE;
                                          """;

                await using var productCommand =
                    new NpgsqlCommand(
                        productSql,
                        connection,
                        transaction);

                productCommand.Parameters.AddWithValue(item.ProductId);

                var res =
                    await productCommand.ExecuteScalarAsync();

                if (res is null)
                {
                    throw new InvalidOperationException(
                        $"Product {item.ProductId} not found.");
                }

                var currentStock = (int)res;

                var newStock =
                    currentStock + item.Quantity;

                const string updateSql = """
                                         UPDATE products
                                         SET stock_quantity = $1
                                         WHERE id = $2;
                                         """;

                await using var updateCommand =
                    new NpgsqlCommand(
                        updateSql,
                        connection,
                        transaction);

                updateCommand.Parameters.AddWithValue(newStock);
                updateCommand.Parameters.AddWithValue(item.ProductId);

                await updateCommand.ExecuteNonQueryAsync();
            }
            
            const string updateOrderSql = """
                                          UPDATE orders
                                          SET status = 'cancelled'
                                          WHERE id = $1;
                                          """;

            await using var updateOrderCommand =
                new NpgsqlCommand(
                    updateOrderSql,
                    connection,
                    transaction);

            updateOrderCommand.Parameters.AddWithValue(orderId);

            await updateOrderCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    public async Task<List<int>> GetExpiredPendingOrderIdsAsync(CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
                           SELECT id
                           FROM orders
                           WHERE status = 'pending'
                             AND created_at <= NOW() - INTERVAL '15 minutes'
                           ORDER BY id;
                           """;

        await using var command =
            new NpgsqlCommand(sql, connection);

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
    await using var connection =
        await _dataSource.OpenConnectionAsync(cancellationToken);

    await using var transaction =
        await connection.BeginTransactionAsync();

    try
    {
        const string orderSql = """
                                SELECT user_id, status, created_at
                                FROM orders
                                WHERE id = $1
                                FOR UPDATE;
                                """;

        await using var orderCommand =
            new NpgsqlCommand(
                orderSql,
                connection,
                transaction);

        orderCommand.Parameters.AddWithValue(orderId);

        await using var reader =
            await orderCommand.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.CloseAsync();
            await transaction.RollbackAsync();

            return null;
        }

        var userId = reader.GetInt32(0);
        var status = reader.GetString(1);
        var createdAt = reader.GetFieldValue<DateTime>(2);

        await reader.CloseAsync();

        if (status != "pending")
        {
            await transaction.RollbackAsync();

            return null;
        }

        if (createdAt > DateTime.UtcNow.AddMinutes(-15))
        {
            await transaction.RollbackAsync();

            return null;
        }

        const string itemsSql = """
                                SELECT product_id, quantity
                                FROM order_items
                                WHERE order_id = $1
                                ORDER BY product_id
                                FOR UPDATE;
                                """;

        await using var itemsCommand =
            new NpgsqlCommand(
                itemsSql,
                connection,
                transaction);

        itemsCommand.Parameters.AddWithValue(orderId);

        var items = new List<(int ProductId, int Quantity)>();

        await using var itemsReader =
            await itemsCommand.ExecuteReaderAsync(cancellationToken);

        while (await itemsReader.ReadAsync(cancellationToken))
        {
            items.Add(
                (
                    itemsReader.GetInt32(0),
                    itemsReader.GetInt32(1)
                ));
        }

        await itemsReader.CloseAsync();

        foreach (var item in items)
        {
            var product =
                await GetProductForUpdateAsync(
                    connection,
                    transaction,
                    item.ProductId);

            if (product is null)
            {
                throw new ProductNotFoundException(
                    item.ProductId);
            }

            var newStockQuantity =
                product.StockQuantity + item.Quantity;

            await UpdateStockAsync(
                connection,
                transaction,
                item.ProductId,
                newStockQuantity);
        }

        const string updateOrderSql = """
                                      UPDATE orders
                                      SET status = 'cancelled'
                                      WHERE id = $1;
                                      """;

        await using var updateOrderCommand =
            new NpgsqlCommand(
                updateOrderSql,
                connection,
                transaction);

        updateOrderCommand.Parameters.AddWithValue(orderId);

        await updateOrderCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync();

        return userId;
    }
    catch
    {
        await transaction.RollbackAsync();

        throw;
    }
}
}