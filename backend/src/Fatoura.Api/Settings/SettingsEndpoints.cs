using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Domain.Numbering;
using Fatoura.Domain.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Settings;

public sealed record SettingsDto(
    string Name,
    string Address,
    Emirate Emirate,
    string Phone,
    string Email,
    string Website,
    string Trn,
    decimal VatRate,
    string PaymentTerms,
    string CompletionOfWork,
    string Notes,
    string ClosingText,
    bool AllowNegativeStock,
    int QuotationValidityDays,
    bool HasLogo,
    bool HasStamp,
    bool IsComplete,
    DateTimeOffset UpdatedAt);

public sealed record UpdateSettingsRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(500)] string Address,
    Emirate Emirate,
    [property: MaxLength(50)] string? Phone,
    [property: MaxLength(256)] string? Email,
    [property: MaxLength(200)] string? Website,
    [property: Required] string Trn,
    [property: Range(0, 1)] decimal VatRate,
    [property: MaxLength(1000)] string? PaymentTerms,
    [property: MaxLength(1000)] string? CompletionOfWork,
    [property: MaxLength(4000)] string? Notes,
    [property: MaxLength(1000)] string? ClosingText,
    bool AllowNegativeStock,
    [property: Range(1, 365)] int QuotationValidityDays);

public sealed record NumberingDto(DocumentType DocumentType, string Pattern, SequenceReset Reset, string Example);

public sealed record UpdateNumberingRequest(DocumentType DocumentType, [property: Required] string Pattern, SequenceReset Reset);

public static class SettingsEndpoints
{
    public const int MaxImageBytes = 1024 * 1024;

    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/settings").WithTags("Settings");

        // Staff can read settings: document forms need the VAT rate and default terms.
        group.MapGet("/", Get).RequireAuthorization(Policies.Staff);
        group.MapPut("/", Update).RequireAuthorization(Policies.Admin);
        group.MapGet("/numbering", GetNumbering).RequireAuthorization(Policies.Admin);
        group.MapPut("/numbering", UpdateNumbering).RequireAuthorization(Policies.Admin);

        // The logo is shown on the login page and app header, so it is public.
        group.MapGet("/logo", (SettingsService s, CancellationToken ct) => GetImage(s, logo: true, ct)).AllowAnonymous();
        group.MapPut("/logo", (IFormFile file, FatouraDbContext db, TimeProvider t, CancellationToken ct) => PutImage(file, db, t, logo: true, ct))
            .RequireAuthorization(Policies.Admin).DisableAntiforgery();
        group.MapDelete("/logo", (FatouraDbContext db, TimeProvider t, CancellationToken ct) => DeleteImage(db, t, logo: true, ct))
            .RequireAuthorization(Policies.Admin);

        group.MapGet("/stamp", (SettingsService s, CancellationToken ct) => GetImage(s, logo: false, ct)).RequireAuthorization(Policies.Admin);
        group.MapPut("/stamp", (IFormFile file, FatouraDbContext db, TimeProvider t, CancellationToken ct) => PutImage(file, db, t, logo: false, ct))
            .RequireAuthorization(Policies.Admin).DisableAntiforgery();
        group.MapDelete("/stamp", (FatouraDbContext db, TimeProvider t, CancellationToken ct) => DeleteImage(db, t, logo: false, ct))
            .RequireAuthorization(Policies.Admin);
        return group;
    }

    public static SettingsDto ToDto(CompanySettings s) => new(
        s.Name, s.Address, s.Emirate, s.Phone, s.Email, s.Website, s.Trn, s.VatRate,
        s.PaymentTerms, s.CompletionOfWork, s.Notes, s.ClosingText, s.AllowNegativeStock, s.QuotationValidityDays,
        s.Logo is not null, s.Stamp is not null, SettingsService.IsComplete(s), s.UpdatedAt);

    private static async Task<Ok<SettingsDto>> Get(SettingsService settings, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await settings.GetAsync(ct)));

    private static async Task<Ok<SettingsDto>> Update(
        UpdateSettingsRequest r, SettingsService settings, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        var trn = TrnValidator.Normalize(r.Trn);
        new FieldValidator()
            .Require(TrnValidator.IsValid(trn), "trn", "TRN must be 15 digits starting with 10.")
            .Require(string.IsNullOrWhiteSpace(r.Email) || new EmailAddressAttribute().IsValid(r.Email.Trim()), "email", "Email is not valid.")
            .ThrowIfInvalid();

        var s = await settings.GetAsync(ct);
        s.Name = r.Name.Trim();
        s.Address = r.Address.Trim();
        s.Emirate = r.Emirate;
        s.Phone = r.Phone?.Trim() ?? string.Empty;
        s.Email = r.Email?.Trim() ?? string.Empty;
        s.Website = r.Website?.Trim() ?? string.Empty;
        s.Trn = trn;
        s.VatRate = r.VatRate;
        s.PaymentTerms = r.PaymentTerms?.Trim() ?? string.Empty;
        s.CompletionOfWork = r.CompletionOfWork?.Trim() ?? string.Empty;
        s.Notes = r.Notes?.Trim() ?? string.Empty;
        s.ClosingText = r.ClosingText?.Trim() ?? string.Empty;
        s.AllowNegativeStock = r.AllowNegativeStock;
        s.QuotationValidityDays = r.QuotationValidityDays;
        s.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(s));
    }

    private static async Task<Ok<List<NumberingDto>>> GetNumbering(FatouraDbContext db, BusinessClock clock, CancellationToken ct)
    {
        var rows = await db.NumberingSettings.AsNoTracking().ToListAsync(ct);
        return TypedResults.Ok(rows.OrderBy(r => r.DocumentType).Select(r => ToDto(r, clock.Today)).ToList());
    }

    private static async Task<Ok<List<NumberingDto>>> UpdateNumbering(
        List<UpdateNumberingRequest> requests, FatouraDbContext db, BusinessClock clock, CancellationToken ct)
    {
        var v = new FieldValidator();
        foreach (var (r, i) in requests.Select((r, i) => (r, i)))
        {
            foreach (var error in NumberPatternFormatter.Validate(r.Pattern?.Trim(), r.Reset))
            {
                v.Add($"[{i}].pattern", error);
            }
        }

        v.ThrowIfInvalid();
        var rows = await db.NumberingSettings.ToListAsync(ct);
        foreach (var r in requests)
        {
            var row = rows.SingleOrDefault(x => x.DocumentType == r.DocumentType);
            if (row is null)
            {
                db.NumberingSettings.Add(row = new NumberingSetting { DocumentType = r.DocumentType });
                rows.Add(row);
            }

            row.Pattern = r.Pattern.Trim();
            row.Reset = r.Reset;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(rows.OrderBy(r => r.DocumentType).Select(r => ToDto(r, clock.Today)).ToList());
    }

    private static NumberingDto ToDto(NumberingSetting n, DateOnly today) =>
        new(n.DocumentType, n.Pattern, n.Reset, NumberPatternFormatter.Format(n.Pattern, today, 1));

    private static async Task<Results<FileContentHttpResult, NotFound>> GetImage(SettingsService settings, bool logo, CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        var (bytes, type) = logo ? (s.Logo, s.LogoContentType) : (s.Stamp, s.StampContentType);
        return bytes is null ? TypedResults.NotFound() : TypedResults.File(bytes, type ?? "application/octet-stream");
    }

    private static async Task<NoContent> PutImage(IFormFile file, FatouraDbContext db, TimeProvider time, bool logo, CancellationToken ct)
    {
        if (file.Length is 0 or > MaxImageBytes)
        {
            throw InvalidRequestException.For("file", "Image must be between 1 byte and 1 MB.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var type = ImageSniffer.Detect(bytes)
            ?? throw InvalidRequestException.For("file", "Only PNG and JPEG images are supported.");

        var s = await db.CompanySettings.SingleAsync(x => x.Id == CompanySettings.SingletonId, ct);
        if (logo)
        {
            (s.Logo, s.LogoContentType) = (bytes, type);
        }
        else
        {
            (s.Stamp, s.StampContentType) = (bytes, type);
        }

        s.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteImage(FatouraDbContext db, TimeProvider time, bool logo, CancellationToken ct)
    {
        var s = await db.CompanySettings.SingleAsync(x => x.Id == CompanySettings.SingletonId, ct);
        if (logo)
        {
            (s.Logo, s.LogoContentType) = (null, null);
        }
        else
        {
            (s.Stamp, s.StampContentType) = (null, null);
        }

        s.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}

public static class ImageSniffer
{
    /// <summary>Identifies PNG/JPEG by magic bytes (the client-supplied content type is not trusted).</summary>
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return null;
    }
}
