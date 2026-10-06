using Microsoft.AspNetCore.Identity;

namespace Fatoura.Api.Data.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>"en" or "ar".</summary>
    public string PreferredLanguage { get; set; } = "en";

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AppUser? User { get; set; }

    /// <summary>SHA-256 of the opaque token, hex encoded. The token itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>All tokens produced by rotating one login share a family; reuse of a rotated token revokes the family.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }
}
