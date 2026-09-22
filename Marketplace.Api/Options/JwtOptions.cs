using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters long.")]
    public string Key { get; init; } = string.Empty;

    [Range(1, 24, ErrorMessage = "Jwt:ExpirationHours must be between 1 and 24.")]
    public int ExpirationHours { get; init; } = 1;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;
}