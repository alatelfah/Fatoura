using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Fatoura.Api.Auth;

public sealed record LoginRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MaxLength(128)] string Password,
    ClientKind Client = ClientKind.Web);

/// <summary>Web clients receive the refresh token as an httpOnly cookie; mobile clients receive it in the body.</summary>
public enum ClientKind
{
    Web = 0,
    Mobile = 1,
}

public sealed record RefreshRequest(string? RefreshToken);

public sealed record ChangePasswordRequest(
    [property: Required] string CurrentPassword,
    [property: Required, MinLength(8), MaxLength(128)] string NewPassword);

public sealed record UpdateProfileRequest([property: Required, RegularExpression("^(en|ar)$")] string PreferredLanguage);

public sealed record UserInfo(Guid Id, string Email, string DisplayName, string Role, string PreferredLanguage);

public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserInfo User, string? RefreshToken);

public static class AuthEndpoints
{
    public const string RefreshCookie = "fatoura_rt";
    private const string CookiePath = "/api/auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", Login).AllowAnonymous();
        group.MapPost("/refresh", Refresh).AllowAnonymous();
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapGet("/me", Me).RequireAuthorization(Policies.Staff);
        group.MapPut("/me", UpdateProfile).RequireAuthorization(Policies.Staff);
        group.MapPost("/change-password", ChangePassword).RequireAuthorization(Policies.Staff);
        return group;
    }

    public static async Task<UserInfo> ToUserInfoAsync(this UserManager<AppUser> users, AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var role = roles.Contains(Roles.Admin) ? Roles.Admin : roles.FirstOrDefault() ?? Roles.Cashier;
        return new UserInfo(user.Id, user.Email ?? string.Empty, user.DisplayName, role, user.PreferredLanguage);
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> Login(
        LoginRequest request, UserManager<AppUser> users, TokenService tokens, HttpContext http, CancellationToken ct)
    {
        var invalid = TypedResults.Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return invalid;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return TypedResults.Problem(
                title: "Account temporarily locked after too many failed attempts. Try again later.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return invalid;
        }

        if (!user.IsActive)
        {
            return TypedResults.Problem(title: "This account is disabled.", statusCode: StatusCodes.Status403Forbidden);
        }

        await users.ResetAccessFailedCountAsync(user);
        var issued = await tokens.IssueAsync(user, ct: ct);
        return TypedResults.Ok(await RespondAsync(users, user, issued, request.Client, http));
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> Refresh(
        RefreshRequest? request, UserManager<AppUser> users, TokenService tokens, HttpContext http, CancellationToken ct)
    {
        var fromBody = request?.RefreshToken;
        var token = fromBody ?? http.Request.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(token))
        {
            return TypedResults.Problem(title: "Not signed in.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await tokens.RefreshAsync(token, ct);
        if (result is not { } r)
        {
            http.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = CookiePath });
            return TypedResults.Problem(title: "Session expired. Please sign in again.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var kind = fromBody is null ? ClientKind.Web : ClientKind.Mobile;
        return TypedResults.Ok(await RespondAsync(users, r.User, r.Tokens, kind, http));
    }

    private static async Task<NoContent> Logout(RefreshRequest? request, TokenService tokens, HttpContext http, CancellationToken ct)
    {
        var token = request?.RefreshToken ?? http.Request.Cookies[RefreshCookie];
        if (!string.IsNullOrEmpty(token))
        {
            await tokens.RevokeAsync(token, ct);
        }

        http.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = CookiePath });
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<UserInfo>, NotFound>> Me(ICurrentUser current, UserManager<AppUser> users)
    {
        var user = await users.FindByIdAsync(current.RequireId().ToString());
        return user is null ? TypedResults.NotFound() : TypedResults.Ok(await users.ToUserInfoAsync(user));
    }

    private static async Task<Results<Ok<UserInfo>, NotFound>> UpdateProfile(
        UpdateProfileRequest request, ICurrentUser current, UserManager<AppUser> users)
    {
        var user = await users.FindByIdAsync(current.RequireId().ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        user.PreferredLanguage = request.PreferredLanguage;
        await users.UpdateAsync(user);
        return TypedResults.Ok(await users.ToUserInfoAsync(user));
    }

    private static async Task<Results<NoContent, NotFound>> ChangePassword(
        ChangePasswordRequest request, ICurrentUser current, UserManager<AppUser> users)
    {
        var user = await users.FindByIdAsync(current.RequireId().ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        result.ThrowIfFailed("newPassword");
        return TypedResults.NoContent();
    }

    private static async Task<AuthResponse> RespondAsync(
        UserManager<AppUser> users, AppUser user, IssuedTokens issued, ClientKind kind, HttpContext http)
    {
        if (kind == ClientKind.Web)
        {
            http.Response.Cookies.Append(RefreshCookie, issued.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = CookiePath,
                Expires = issued.RefreshTokenExpiresAt,
            });
        }

        return new AuthResponse(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            await users.ToUserInfoAsync(user),
            kind == ClientKind.Mobile ? issued.RefreshToken : null);
    }
}

internal static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result, string field)
    {
        if (!result.Succeeded)
        {
            throw new InvalidRequestException(new Dictionary<string, string[]>
            {
                [field] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }
    }
}
