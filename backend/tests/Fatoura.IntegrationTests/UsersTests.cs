using System.Net;
using System.Net.Http.Json;
using Fatoura.Api.Auth;
using Fatoura.Api.Users;
using Fatoura.IntegrationTests.Infrastructure;

namespace Fatoura.IntegrationTests;

public class UsersTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Admin_creates_cashier_who_cannot_manage_users()
    {
        var (cashier, _, _) = await api.NewCashierAsync();
        (await cashier.GetAsync("/api/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cashier.PostAsJsonAsync("/api/users", new { email = "x@test.local", displayName = "X", password = "Passw0rd!", role = "Admin" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var admin = await api.AdminAsync();
        var list = await (await admin.GetAsync("/api/users")).ReadAsync<List<UserDto>>();
        list.ShouldContain(u => u.Role == Roles.Cashier);
    }

    [Fact]
    public async Task Duplicate_email_and_weak_password_are_validation_errors()
    {
        var admin = await api.AdminAsync();
        var dup = await admin.PostAsJsonAsync("/api/users", new { email = ApiFactory.AdminEmail, displayName = "Dup", password = "Passw0rd!", role = "Cashier" });
        (await dup.ReadJsonAsync(HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();

        var weak = await admin.PostAsJsonAsync("/api/users", new { email = "weak@test.local", displayName = "Weak", password = "alllowercase", role = "Cashier" });
        weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var badRole = await admin.PostAsJsonAsync("/api/users", new { email = "role@test.local", displayName = "R", password = "Passw0rd!", role = "Owner" });
        badRole.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_or_demote_themselves()
    {
        var admin = await api.AdminAsync();
        var me = await (await admin.GetAsync("/api/auth/me")).ReadAsync<UserInfo>();
        var r = await admin.PutAsJsonAsync($"/api/users/{me.Id}", new { displayName = "Me", role = "Cashier", isActive = true });
        r.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Promoting_a_cashier_takes_effect_after_refresh_and_reset_password_works()
    {
        var (_, id, email) = await api.NewCashierAsync();
        var admin = await api.AdminAsync();
        var updated = await (await admin.PutAsJsonAsync($"/api/users/{id}", new { displayName = "Promoted", role = "Admin", isActive = true }))
            .ReadAsync<UserDto>();
        updated.Role.ShouldBe(Roles.Admin);

        (await admin.PostAsJsonAsync($"/api/users/{id}/reset-password", new { newPassword = "Reset@12345" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var promoted = await api.LoginAsync(email, "Reset@12345");
        (await promoted.GetAsync("/api/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
