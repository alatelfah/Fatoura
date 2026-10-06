using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Fatoura.IntegrationTests.Infrastructure;

/// <summary>
/// One SQL Server for the whole test run: FATOURA_TEST_SQL (a server-level connection string, e.g. a local
/// container or the CI service) when set, otherwise a Testcontainers SQL Server. Each test class gets its own database.
/// </summary>
public static class SqlServer
{
    private static readonly Lazy<Task<string>> Server = new(StartAsync);
    private static MsSqlContainer? _container;

    public static async Task<string> ConnectionStringForAsync(string database)
    {
        var server = await Server.Value;
        return new SqlConnectionStringBuilder(server) { InitialCatalog = database, TrustServerCertificate = true }.ConnectionString;
    }

    public static async Task DropDatabaseAsync(string database)
    {
        var server = await Server.Value;
        await using var conn = new SqlConnection(new SqlConnectionStringBuilder(server) { InitialCatalog = "master", TrustServerCertificate = true }.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            IF DB_ID(N'{database}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{database}];
            END
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string> StartAsync()
    {
        var configured = Environment.GetEnvironmentVariable("FATOURA_TEST_SQL");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        return _container.GetConnectionString();
    }
}
