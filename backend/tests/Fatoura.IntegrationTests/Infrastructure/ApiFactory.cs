using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Fatoura.IntegrationTests.Infrastructure;

/// <summary>Runs the real API in-memory against a fresh SQL Server database (one per test class).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin@12345";
    public const string CashierPassword = "Cashier@12345";

    public string DatabaseName { get; } = "fatoura_test_" + Guid.NewGuid().ToString("N")[..12];

    private string _connectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _connectionString = await SqlServer.ConnectionStringForAsync(DatabaseName);
        ClientOptions.BaseAddress = new Uri("https://localhost");
        _ = Server; // start the host: runs migrations and seeding
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await SqlServer.DropDatabaseAsync(DatabaseName);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Fatoura", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:AdminName", "Test Admin");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }

    public async Task<T> WithDbAsync<T>(Func<FatouraDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<FatouraDbContext>());
    }

    public async Task<HttpClient> AdminAsync() => await LoginAsync(AdminEmail, AdminPassword);

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password, client = "Mobile" });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Creates a new cashier (via the API, as Admin) and returns a client signed in as them.</summary>
    public async Task<(HttpClient Client, Guid Id, string Email)> NewCashierAsync(string? name = null)
    {
        var admin = await AdminAsync();
        var email = $"cashier-{Guid.NewGuid().ToString("N")[..12]}@test.local";
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            email,
            displayName = name ?? "Test Cashier",
            password = CashierPassword,
            role = Roles.Cashier,
        });
        response.EnsureSuccessStatusCode();
        var user = (await response.Content.ReadFromJsonAsync<Fatoura.Api.Users.UserDto>(Json.Options))!;
        return (await LoginAsync(email, CashierPassword), user.Id, email);
    }
}
