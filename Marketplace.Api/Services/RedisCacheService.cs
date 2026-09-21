using StackExchange.Redis;

namespace Marketplace.Api.Services;

public class RedisCacheService
{
    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task SetAsync(
        string key,
        string value,
        TimeSpan expiration)
    {
        await _database.StringSetAsync(
            key,
            value,
            expiration);
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _database.StringGetAsync(key);

        return value.HasValue
            ? value.ToString()
            : null;
    }

    public async Task RemoveAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }
}