using System.Security.Claims;
using Fatoura.Api.Auth;

namespace Fatoura.Api.Infrastructure;

public interface ICurrentUser
{
    Guid? Id { get; }

    string Name { get; }

    bool IsAdmin { get; }

    Guid RequireId();
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimNames.Subject), out var id) ? id : null;

    public string Name => Principal?.FindFirstValue(ClaimNames.Email) ?? "system";

    public bool IsAdmin => Principal?.IsInRole(Roles.Admin) ?? false;

    public Guid RequireId() => Id ?? throw new ForbiddenException("An authenticated user is required.");
}
