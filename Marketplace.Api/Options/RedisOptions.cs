namespace Marketplace.Api.Options;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = string.Empty;
}