using Fatoura.Api.Auth;
using Fatoura.Api.Data.Entities;
using Fatoura.Domain.Numbering;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Data;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Email of the first Admin, created only when no Admin exists yet.</summary>
    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string AdminName { get; set; } = "Administrator";
}

public static class DbInitializer
{
    /// <summary>How long start-up waits for SQL Server, which may still be starting (e.g. under docker compose).</summary>
    public static readonly TimeSpan ServerStartupTimeout = TimeSpan.FromMinutes(3);

    public const string DefaultClosingText =
        "We hope that you will find our price most competitive and look forward to your valuable order. " +
        "For any clarification, feel free to contact us. Assuring you of our best services.";

    public static async Task InitializeAsync(IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<FatouraDbContext>();
        var time = sp.GetRequiredService<TimeProvider>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

        await WaitForServerAsync(db, migrate, logger, time, ct);

        var roles = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        if (!await db.CompanySettings.AnyAsync(ct))
        {
            db.CompanySettings.Add(new CompanySettings
            {
                ClosingText = DefaultClosingText,
                UpdatedAt = time.GetUtcNow(),
            });
        }

        var existing = await db.NumberingSettings.Select(n => n.DocumentType).ToListAsync(ct);
        foreach (var type in Enum.GetValues<DocumentType>().Except(existing))
        {
            db.NumberingSettings.Add(new NumberingSetting
            {
                DocumentType = type,
                Pattern = NumberPatternFormatter.DefaultPattern(type),
                Reset = SequenceReset.Yearly,
            });
        }

        await db.SaveChangesAsync(ct);

        var seed = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedOptions>>().Value;
        var users = sp.GetRequiredService<UserManager<AppUser>>();
        if ((await users.GetUsersInRoleAsync(Roles.Admin)).Count == 0)
        {
            if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
            {
                logger.LogWarning("No Admin user exists. Set Seed:AdminEmail and Seed:AdminPassword to create one.");
                return;
            }

            var admin = new AppUser
            {
                UserName = seed.AdminEmail,
                Email = seed.AdminEmail,
                EmailConfirmed = true,
                DisplayName = seed.AdminName,
                CreatedAt = time.GetUtcNow(),
                LockoutEnabled = true,
            };
            var created = await users.CreateAsync(admin, seed.AdminPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create the seed Admin: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            }

            await users.AddToRoleAsync(admin, Roles.Admin);
            logger.LogInformation("Created seed Admin {Email}.", seed.AdminEmail);
        }
    }

    /// <summary>
    /// Applies migrations (or just checks the connection), retrying while SQL Server is unreachable or still starting,
    /// for up to <see cref="ServerStartupTimeout"/>. Each failure is logged, so a wrong password shows up in the log.
    /// </summary>
    private static async Task WaitForServerAsync(FatouraDbContext db, bool migrate, ILogger logger, TimeProvider time, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (migrate)
                {
                    await db.Database.MigrateAsync(ct);
                    return;
                }

                if (await db.Database.CanConnectAsync(ct))
                {
                    return;
                }

                throw new InvalidOperationException("Cannot connect to the database.");
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException
                && time.GetElapsedTime(started) < ServerStartupTimeout)
            {
                logger.LogWarning("Database not reachable yet (attempt {Attempt}): {Message}. Retrying in 5 seconds.", attempt, ex.GetBaseException().Message);
                await Task.Delay(TimeSpan.FromSeconds(5), time, ct);
            }
        }
    }
}
