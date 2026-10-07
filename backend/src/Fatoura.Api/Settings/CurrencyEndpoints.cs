using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Domain.Documents;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Settings;

/// <summary>A foreign currency and its rate in AED per unit.</summary>
public sealed record CurrencyDto(string Code, string Name, decimal RateToAed, bool IsActive, DateTimeOffset UpdatedAt);

public sealed record CurrencyRequest([property: Required, MaxLength(100)] string Name, decimal RateToAed, bool IsActive = true);

/// <summary>The currency and rate a document is issued in.</summary>
public sealed record DocumentCurrency(string Code, decimal Rate)
{
    public static readonly DocumentCurrency Aed = new(CurrencyConverter.Base, 1m);
}

public static class CurrencyEndpoints
{
    public static RouteGroupBuilder MapCurrencyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/settings/currencies").WithTags("Settings");

        // Staff read the list: document forms offer the currencies and pre-fill their rates.
        group.MapGet("/", List).RequireAuthorization(Policies.Staff);
        group.MapPut("/{code}", Upsert).RequireAuthorization(Policies.Admin);
        group.MapDelete("/{code}", Delete).RequireAuthorization(Policies.Admin);
        return group;
    }

    private static CurrencyDto ToDto(CurrencyRate c) => new(c.Code, c.Name, c.RateToAed, c.IsActive, c.UpdatedAt);

    private static async Task<Ok<List<CurrencyDto>>> List(FatouraDbContext db, CancellationToken ct) =>
        TypedResults.Ok((await db.CurrencyRates.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct)).Select(ToDto).ToList());

    private static async Task<Ok<CurrencyDto>> Upsert(string code, CurrencyRequest r, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        code = code.Trim().ToUpperInvariant();
        new FieldValidator()
            .Require(CurrencyConverter.IsValidCode(code), "code", "Use the 3-letter ISO code, e.g. USD.")
            .Require(code != CurrencyConverter.Base, "code", "AED is the base currency and needs no rate.")
            .Require(!string.IsNullOrWhiteSpace(r.Name), "name", "Name is required.")
            .Require(CurrencyConverter.IsValidRate(r.RateToAed), "rateToAed", "The rate must be positive with at most 6 decimals.")
            .ThrowIfInvalid();

        var c = await db.CurrencyRates.SingleOrDefaultAsync(x => x.Code == code, ct);
        if (c is null)
        {
            db.CurrencyRates.Add(c = new CurrencyRate { Code = code });
        }

        c.Name = r.Name.Trim();
        c.RateToAed = r.RateToAed;
        c.IsActive = r.IsActive;
        c.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(c));
    }

    /// <summary>A currency used on any document is deactivated instead, so those documents keep a valid currency.</summary>
    private static async Task<NoContent> Delete(string code, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        code = code.Trim().ToUpperInvariant();
        var c = await db.CurrencyRates.SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw new NotFoundException("Currency");
        var used = await db.Quotations.AnyAsync(d => d.Currency == code, ct) || await db.Invoices.AnyAsync(d => d.Currency == code, ct)
            || await db.PurchaseInvoices.AnyAsync(d => d.Currency == code, ct);
        if (used)
        {
            c.IsActive = false;
            c.UpdatedAt = time.GetUtcNow();
        }
        else
        {
            db.CurrencyRates.Remove(c);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Resolves a document's currency. AED always has rate 1. Another currency must be set up in Settings, and active
    /// unless <paramref name="existing"/> (the document being edited) already uses it. Omitting the currency keeps the
    /// existing one (AED for a new document); omitting the rate keeps the existing rate, or takes the current one from Settings.
    /// </summary>
    public static async Task<DocumentCurrency> ResolveAsync(
        FatouraDbContext db, string? code, decimal? rate, FieldValidator v, CancellationToken ct, ICurrencyDocument? existing = null)
    {
        code = string.IsNullOrWhiteSpace(code) ? existing?.Currency ?? CurrencyConverter.Base : code.Trim().ToUpperInvariant();
        if (code == CurrencyConverter.Base)
        {
            return DocumentCurrency.Aed;
        }

        var c = await db.CurrencyRates.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, ct);
        var keeps = existing?.Currency == code;
        if (c is null || (!c.IsActive && !keeps))
        {
            v.Add("currency", "This currency is not set up in Settings.");
            return DocumentCurrency.Aed;
        }

        var resolved = rate ?? (keeps ? existing!.ExchangeRate : c.RateToAed);
        v.Require(CurrencyConverter.IsValidRate(resolved), "exchangeRate", "The exchange rate must be positive with at most 6 decimals.");
        return new DocumentCurrency(code, resolved);
    }

    /// <summary>The current rate for a currency from Settings, or <paramref name="fallback"/> if it is no longer active.</summary>
    public static async Task<DocumentCurrency> CurrentAsync(FatouraDbContext db, string code, decimal fallback, CancellationToken ct)
    {
        if (code == CurrencyConverter.Base)
        {
            return DocumentCurrency.Aed;
        }

        var rate = await db.CurrencyRates.AsNoTracking().Where(c => c.Code == code && c.IsActive).Select(c => (decimal?)c.RateToAed).SingleOrDefaultAsync(ct);
        return new DocumentCurrency(code, rate ?? fallback);
    }
}
