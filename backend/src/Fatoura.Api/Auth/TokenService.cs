using System.Security.Cryptography;
using System.Text;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fatoura.Api.Auth;

public sealed record IssuedTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Issues short-lived JWT access tokens and opaque rotating refresh tokens.
/// Refresh tokens are stored as SHA-256 hashes; presenting an already-rotated token revokes its whole family
/// (token theft detection). Refresh also re-checks that the user is still active.
/// </summary>
public sealed class TokenService(
    FatouraDbContext db,
    UserManager<AppUser> users,
    IOptions<JwtOptions> options,
    TimeProvider time)
{
    private readonly JwtOptions _options = options.Value;

    public static SymmetricSecurityKey SigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));

    public async Task<IssuedTokens> IssueAsync(AppUser user, Guid? familyId = null, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var roles = await users.GetRolesAsync(user);
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
            [JwtRegisteredClaimNames.Name] = user.DisplayName,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [ClaimNames.Role] = roles.ToArray(),
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            FamilyId = familyId ?? Guid.NewGuid(),
            CreatedAt = now,
            ExpiresAt = refreshExpires,
        });
        await db.SaveChangesAsync(ct);

        return new IssuedTokens(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    /// <summary>Rotates a refresh token. Returns null when the token is unknown, expired, revoked or the user is inactive.</summary>
    public async Task<(AppUser User, IssuedTokens Tokens)?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored?.User is null)
        {
            return null;
        }

        if (stored.RevokedAt is not null)
        {
            if (await IsBenignReuseAsync(stored, now, ct) && stored.User.IsActive)
            {
                // Two refreshes raced with the same token (e.g. two tabs reloading): continue the same login.
                return (stored.User, await IssueAsync(stored.User, stored.FamilyId, ct));
            }

            // A rotated token was presented again: assume it was stolen and revoke every token from that login.
            await RevokeFamilyAsync(stored.FamilyId, ct);
            return null;
        }

        if (stored.ExpiresAt <= now || !stored.User.IsActive || await users.IsLockedOutAsync(stored.User))
        {
            await RevokeFamilyAsync(stored.FamilyId, ct);
            return null;
        }

        var issued = await IssueAsync(stored.User, stored.FamilyId, ct);
        var replacement = await db.RefreshTokens.SingleAsync(t => t.TokenHash == Hash(issued.RefreshToken), ct);
        stored.RevokedAt = now;
        stored.ReplacedById = replacement.Id;
        await db.SaveChangesAsync(ct);
        return (stored.User, issued);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(ct);
        if (familyId is { } id)
        {
            await RevokeFamilyAsync(id, ct);
        }
    }

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default) =>
        db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, time.GetUtcNow()), ct);

    /// <summary>Reuse is benign only shortly after a normal rotation, and only while the login itself is still alive.</summary>
    private async Task<bool> IsBenignReuseAsync(RefreshToken stored, DateTimeOffset now, CancellationToken ct) =>
        stored.ReplacedById is not null
        && stored.RevokedAt is { } revokedAt
        && now - revokedAt <= TimeSpan.FromSeconds(_options.RefreshReuseGraceSeconds)
        && await db.RefreshTokens.AnyAsync(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null && t.ExpiresAt > now, ct);

    private Task RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, time.GetUtcNow()), ct);

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
