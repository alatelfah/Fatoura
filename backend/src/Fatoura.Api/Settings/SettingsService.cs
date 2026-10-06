using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Domain.Validation;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Settings;

public sealed class SettingsService(FatouraDbContext db)
{
    public async Task<CompanySettings> GetAsync(CancellationToken ct = default) =>
        await db.CompanySettings.SingleOrDefaultAsync(s => s.Id == CompanySettings.SingletonId, ct)
        ?? throw new InvalidOperationException("Company settings row is missing; database was not seeded.");

    public static bool IsComplete(CompanySettings s) =>
        !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Address) && TrnValidator.IsValid(s.Trn);

    /// <summary>Tax invoices must show the supplier's name, address and TRN, so issuing requires them.</summary>
    public async Task<CompanySettings> RequireCompleteAsync(CancellationToken ct = default)
    {
        var s = await GetAsync(ct);
        if (!IsComplete(s))
        {
            throw new ConflictException(
                "Complete the company name, address and TRN in Settings before issuing documents.", "settings_incomplete");
        }

        return s;
    }
}
