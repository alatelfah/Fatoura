using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fatoura.Api.Auth;
using Fatoura.IntegrationTests.Infrastructure;

namespace Fatoura.IntegrationTests;

public class AuthTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Admin_can_sign_in_and_read_profile()
    {
        var client = await api.AdminAsync();
        var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<UserInfo>();
        me.Email.ShouldBe(ApiFactory.AdminEmail);
        me.Role.ShouldBe(Roles.Admin);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_and_repeated_failures_lock_the_account()
    {
        var (_, _, email) = await api.NewCashierAsync();
        var client = api.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong@12345" });
            r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.CashierPassword });
        locked.StatusCode.ShouldBe(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task Unknown_user_gets_the_same_error_as_a_wrong_password()
    {
        var r = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.local", password = "Whatever@123" });
        r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mobile_refresh_rotates_and_reuse_revokes_the_whole_family()
    {
        var client = api.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword, client = "Mobile" })).ReadAsync<AuthResponse>();
        login.RefreshToken.ShouldNotBeNullOrEmpty();

        var first = await (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).ReadAsync<AuthResponse>();
        first.RefreshToken.ShouldNotBe(login.RefreshToken);

        // Replaying the rotated token is treated as theft...
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // ...and kills the legitimate successor too.
        var successor = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        successor.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Web_login_uses_an_httponly_cookie_and_never_returns_the_refresh_token()
    {
        var client = api.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword, client = "Web" });
        var login = await response.ReadAsync<AuthResponse>();
        login.RefreshToken.ShouldBeNull();

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AuthEndpoints.RefreshCookie, StringComparison.Ordinal));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldContain("path=/api/auth", Case.Insensitive);

        // The client's cookie container sends it back automatically.
        var refreshed = await (await client.PostAsync("/api/auth/refresh", null)).ReadAsync<AuthResponse>();
        refreshed.AccessToken.ShouldNotBeNullOrEmpty();

        (await client.PostAsync("/api/auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Disabled_user_can_neither_refresh_nor_sign_in()
    {
        var (_, cashierId, email) = await api.NewCashierAsync();
        var client = api.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = ApiFactory.CashierPassword, client = "Mobile" })).ReadAsync<AuthResponse>();

        var admin = await api.AdminAsync();
        (await admin.PutAsJsonAsync($"/api/users/{cashierId}", new { displayName = "Gone", role = Roles.Cashier, isActive = false }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.CashierPassword })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected_and_invalid_tokens_too()
    {
        var client = api.CreateClient();
        (await client.GetAsync("/api/settings")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
        (await client.GetAsync("/api/settings")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_can_change_password_and_language()
    {
        var (cashier, _, email) = await api.NewCashierAsync();
        (await cashier.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = ApiFactory.CashierPassword, newPassword = "Changed@12345" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await api.LoginAsync(email, "Changed@12345");

        var me = await (await cashier.PutAsJsonAsync("/api/auth/me", new { preferredLanguage = "ar" })).ReadAsync<UserInfo>();
        me.PreferredLanguage.ShouldBe("ar");
    }
}
