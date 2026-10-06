using System.ComponentModel.DataAnnotations;

namespace Fatoura.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "fatoura";

    public string Audience { get; set; } = "fatoura";

    /// <summary>HMAC-SHA256 key; at least 32 characters. Must come from configuration/secrets in production.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 14;
}
