using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Auth;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Users;

public sealed record UserDto(Guid Id, string Email, string DisplayName, string Role, bool IsActive, string PreferredLanguage, DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MaxLength(100)] string DisplayName,
    [property: Required, MinLength(8), MaxLength(128)] string Password,
    [property: Required, RegularExpression("^(Admin|Cashier)$")] string Role);

public sealed record UpdateUserRequest(
    [property: Required, MaxLength(100)] string DisplayName,
    [property: Required, RegularExpression("^(Admin|Cashier)$")] string Role,
    bool IsActive);

public sealed record ResetPasswordRequest([property: Required, MinLength(8), MaxLength(128)] string NewPassword);

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/users").WithTags("Users").RequireAuthorization(Policies.Admin);
        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapPost("/{id:guid}/reset-password", ResetPassword);
        return group;
    }

    private static async Task<Ok<List<UserDto>>> List(UserManager<AppUser> users, CancellationToken ct)
    {
        var all = await users.Users.OrderBy(u => u.DisplayName).ToListAsync(ct);
        var result = new List<UserDto>(all.Count);
        foreach (var u in all)
        {
            result.Add(await ToDtoAsync(users, u));
        }

        return TypedResults.Ok(result);
    }

    private static async Task<Created<UserDto>> Create(CreateUserRequest request, UserManager<AppUser> users, TimeProvider time)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            throw InvalidRequestException.For("email", "A user with this email already exists.");
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = time.GetUtcNow(),
            LockoutEnabled = true,
        };
        (await users.CreateAsync(user, request.Password)).ThrowIfFailed("password");
        (await users.AddToRoleAsync(user, request.Role)).ThrowIfFailed("role");
        return TypedResults.Created($"/api/users/{user.Id}", await ToDtoAsync(users, user));
    }

    private static async Task<Results<Ok<UserDto>, NotFound>> Update(
        Guid id, UpdateUserRequest request, UserManager<AppUser> users, ICurrentUser current, TokenService tokens)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var roles = await users.GetRolesAsync(user);
        var wasActiveAdmin = user.IsActive && roles.Contains(Roles.Admin);
        var staysActiveAdmin = request.IsActive && request.Role == Roles.Admin;
        if (id == current.Id && !staysActiveAdmin)
        {
            throw new ConflictException("You cannot deactivate yourself or remove your own Admin role.");
        }

        if (wasActiveAdmin && !staysActiveAdmin)
        {
            var activeAdmins = (await users.GetUsersInRoleAsync(Roles.Admin)).Count(u => u.IsActive);
            if (activeAdmins <= 1)
            {
                throw new ConflictException("At least one active Admin is required.");
            }
        }

        user.DisplayName = request.DisplayName.Trim();
        var deactivated = user.IsActive && !request.IsActive;
        user.IsActive = request.IsActive;
        (await users.UpdateAsync(user)).ThrowIfFailed("user");

        if (!roles.Contains(request.Role))
        {
            (await users.RemoveFromRolesAsync(user, roles)).ThrowIfFailed("role");
            (await users.AddToRoleAsync(user, request.Role)).ThrowIfFailed("role");
        }

        if (deactivated || !roles.Contains(request.Role))
        {
            // Force a fresh sign-in so new permissions (or the lock-out) apply within one access-token lifetime.
            await users.UpdateSecurityStampAsync(user);
            await tokens.RevokeAllForUserAsync(user.Id);
        }

        return TypedResults.Ok(await ToDtoAsync(users, user));
    }

    private static async Task<Results<NoContent, NotFound>> ResetPassword(
        Guid id, ResetPasswordRequest request, UserManager<AppUser> users, TokenService tokens)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var resetToken = await users.GeneratePasswordResetTokenAsync(user);
        (await users.ResetPasswordAsync(user, resetToken, request.NewPassword)).ThrowIfFailed("newPassword");
        await users.SetLockoutEndDateAsync(user, null);
        await tokens.RevokeAllForUserAsync(user.Id);
        return TypedResults.NoContent();
    }

    private static async Task<UserDto> ToDtoAsync(UserManager<AppUser> users, AppUser u)
    {
        var info = await users.ToUserInfoAsync(u);
        return new UserDto(u.Id, info.Email, u.DisplayName, info.Role, u.IsActive, u.PreferredLanguage, u.CreatedAt);
    }
}
