using System.Data;
using Fatoura.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Infrastructure;

public static class DbTransactions
{
    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction under the EF execution strategy
    /// (retried as a whole on transient failures, so the work must load everything it uses).
    /// All document issuing goes through here so numbering, document rows and stock commit atomically.
    /// </summary>
    public static async Task<T> InTransactionAsync<T>(this FatouraDbContext db, Func<Task<T>> work, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                var result = await work();
                await tx.CommitAsync(ct);
                return result;
            });
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 } sql && sql.Message.Contains("Number", StringComparison.Ordinal))
        {
            throw new ConflictException(
                "The generated document number already exists. Check the numbering pattern in Settings.", "duplicate_number");
        }
    }
}
