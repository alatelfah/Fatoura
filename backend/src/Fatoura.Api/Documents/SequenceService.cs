using Fatoura.Api.Data;
using Fatoura.Domain.Numbering;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Documents;

/// <summary>
/// Gap-free document numbers. The counter row is incremented with a single <c>UPDATE … OUTPUT</c> inside the
/// caller's transaction: the row lock is held until commit, so concurrent issues serialise and a rolled-back
/// document gives its number back. Must be called inside <see cref="Infrastructure.DbTransactions.InTransactionAsync{T}"/>.
/// Lock order across the app: sequence → document → item stock.
/// </summary>
public sealed class SequenceService(FatouraDbContext db)
{
    private sealed record SequenceValue(long Value);

    public async Task<string> NextNumberAsync(DocumentType type, DateOnly date, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Document numbers must be allocated inside a transaction.");
        }

        var setting = await db.NumberingSettings.AsNoTracking().SingleAsync(n => n.DocumentType == type, ct);
        var key = NumberPatternFormatter.ResetKey(setting.Reset, date);
        var typeName = type.ToString();

        // Create the counter for a new period without racing another transaction doing the same.
        await db.Database.ExecuteSqlAsync($"""
            MERGE [DocumentSequences] WITH (HOLDLOCK) AS t
            USING (SELECT {typeName} AS [DocumentType], {key} AS [ResetKey]) AS s
                ON t.[DocumentType] = s.[DocumentType] AND t.[ResetKey] = s.[ResetKey]
            WHEN NOT MATCHED THEN INSERT ([DocumentType], [ResetKey], [NextValue]) VALUES (s.[DocumentType], s.[ResetKey], 1);
            """, ct);

        var rows = await db.Database.SqlQuery<SequenceValue>($"""
            UPDATE [DocumentSequences]
            SET [NextValue] = [NextValue] + 1
            OUTPUT deleted.[NextValue] AS [Value]
            WHERE [DocumentType] = {typeName} AND [ResetKey] = {key}
            """).ToListAsync(ct);

        return NumberPatternFormatter.Format(setting.Pattern, date, rows.Single().Value);
    }
}
