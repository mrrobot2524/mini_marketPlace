namespace Marketplace.Api.Options;

public class PostgresOptions
{
    public const string SectionName = "ConnectionStrings";

    public string DefaultConnection { get; init; } = string.Empty;
}