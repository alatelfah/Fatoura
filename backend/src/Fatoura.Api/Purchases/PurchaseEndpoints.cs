using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Documents;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Inventory;
using Fatoura.Api.Settings;
using Fatoura.Domain.Numbering;
using Fatoura.Domain.Tax;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Purchases;

/// <summary>A purchase line: an item (adds stock at this unit cost) or an expense with a free-text category.</summary>
public sealed record PurchaseLineRequest(
    int? ItemId,
    string Description,
    [property: MaxLength(100)] string? ExpenseCategory,
    decimal Quantity,
    decimal UnitPrice,
    TaxCategory TaxCategory);

public sealed record PurchaseRequest(
    int SupplierId,
    [property: MaxLength(60)] string? SupplierInvoiceNo,
    DateOnly Date,
    [property: MaxLength(2000)] string? Notes,
    List<PurchaseLineRequest> Lines);

public sealed record PurchaseLineDto(
    int Id,
    int LineNo,
    int? ItemId,
    string Description,
    string ExpenseCategory,
    decimal Quantity,
    decimal UnitPrice,
    TaxCategory TaxCategory,
    decimal VatRate,
    decimal Net,
    decimal Vat,
    decimal Total);

public sealed record PurchaseDto(
    int Id,
    string Number,
    string SupplierInvoiceNo,
    DateOnly Date,
    int SupplierId,
    PartyDto Supplier,
    string Notes,
    decimal SubTotal,
    decimal VatTotal,
    decimal Total,
    bool HasAttachment,
    string? AttachmentFileName,
    DateTimeOffset CreatedAt,
    List<PurchaseLineDto> Lines);

public sealed record PurchaseSummaryDto(
    int Id, string Number, string SupplierInvoiceNo, DateOnly Date, int SupplierId, string SupplierName, decimal SubTotal, decimal VatTotal, decimal Total);

public static class PurchaseEndpoints
{
    public const int MaxAttachmentBytes = 5 * 1024 * 1024;

    public static RouteGroupBuilder MapPurchaseEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/purchases").WithTags("Purchases").RequireAuthorization(Policies.Admin);
        group.MapGet("/", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
        group.MapGet("/{id:int}/attachment", GetAttachment);
        group.MapPut("/{id:int}/attachment", PutAttachment).DisableAntiforgery();
        group.MapDelete("/{id:int}/attachment", DeleteAttachment);
        return group;
    }

    private static async Task<PurchaseDto> LoadDtoAsync(FatouraDbContext db, int id, CancellationToken ct)
    {
        var p = await db.PurchaseInvoices.AsNoTracking().Include(x => x.Lines)
            .Select(x => new { P = x, HasAttachment = x.Attachment != null })
            .SingleOrDefaultAsync(x => x.P.Id == id, ct) ?? throw new NotFoundException("Purchase");
        var x = p.P;
        return new PurchaseDto(
            x.Id, x.Number, x.SupplierInvoiceNo, x.Date, x.SupplierId, x.SupplierSnapshot.ToDto(), x.Notes, x.SubTotal, x.VatTotal, x.Total,
            p.HasAttachment, x.AttachmentFileName, x.CreatedAt,
            x.Lines.OrderBy(l => l.LineNo).Select(l => new PurchaseLineDto(
                l.Id, l.LineNo, l.ItemId, l.Description, l.ExpenseCategory, l.Quantity, l.UnitPrice, l.TaxCategory, l.VatRate, l.Net, l.Vat, l.Total)).ToList());
    }

    private static async Task<Ok<PagedResult<PurchaseSummaryDto>>> List(
        FatouraDbContext db, string? search, int? supplierId, DateOnly? from, DateOnly? to, int? page, int? pageSize, CancellationToken ct)
    {
        var q = db.PurchaseInvoices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = Paging.Like(search);
            q = q.Where(p => EF.Functions.Like(p.Number, like) || EF.Functions.Like(p.SupplierInvoiceNo, like) || EF.Functions.Like(p.SupplierSnapshot.Name, like));
        }

        if (supplierId is { } s)
        {
            q = q.Where(p => p.SupplierId == s);
        }

        if (from is { } f)
        {
            q = q.Where(p => p.Date >= f);
        }

        if (to is { } t)
        {
            q = q.Where(p => p.Date <= t);
        }

        var result = await q.OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
            .Select(p => new PurchaseSummaryDto(p.Id, p.Number, p.SupplierInvoiceNo, p.Date, p.SupplierId, p.SupplierSnapshot.Name, p.SubTotal, p.VatTotal, p.Total))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<PurchaseDto>> Get(int id, FatouraDbContext db, CancellationToken ct) => TypedResults.Ok(await LoadDtoAsync(db, id, ct));

    private static async Task<Created<PurchaseDto>> Create(
        PurchaseRequest r,
        FatouraDbContext db,
        SequenceService sequences,
        StockService stock,
        SettingsService settingsService,
        ICurrentUser user,
        BusinessClock clock,
        TimeProvider time,
        CancellationToken ct)
    {
        await ValidateAsync(r, db, clock, ct);
        var id = await db.InTransactionAsync(async () =>
        {
            var settings = await settingsService.GetAsync(ct);
            var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == r.SupplierId, ct)
                ?? throw InvalidRequestException.For("supplierId", "Supplier not found.");
            var now = time.GetUtcNow();
            var p = new PurchaseInvoice
            {
                Number = await sequences.NextNumberAsync(DocumentType.Purchase, r.Date, ct),
                CreatedById = user.RequireId(),
                CreatedAt = now,
            };
            Apply(p, r, supplier, settings.VatRate, now);
            db.PurchaseInvoices.Add(p);
            await db.SaveChangesAsync(ct);
            await stock.ApplyAsync(StockChanges(p, p.Lines, +1, StockMovementType.Purchase), allowNegative: true, ct);
            await db.SaveChangesAsync(ct);
            return p.Id;
        }, ct);
        return TypedResults.Created($"/api/purchases/{id}", await LoadDtoAsync(db, id, ct));
    }

    private static async Task<Ok<PurchaseDto>> Update(
        int id, PurchaseRequest r, FatouraDbContext db, StockService stock, SettingsService settingsService, BusinessClock clock, TimeProvider time, CancellationToken ct)
    {
        await ValidateAsync(r, db, clock, ct);
        await db.InTransactionAsync(async () =>
        {
            var settings = await settingsService.GetAsync(ct);
            var p = await db.PurchaseInvoices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Purchase");
            var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == r.SupplierId, ct)
                ?? throw InvalidRequestException.For("supplierId", "Supplier not found.");
            var oldLines = p.Lines.ToList();
            await stock.ApplyAsync(StockChanges(p, oldLines, -1, StockMovementType.PurchaseReversal), allowNegative: true, ct);
            db.PurchaseLines.RemoveRange(oldLines);
            p.Lines = [];
            Apply(p, r, supplier, settings.VatRate, time.GetUtcNow());
            await db.SaveChangesAsync(ct);
            await stock.ApplyAsync(StockChanges(p, p.Lines, +1, StockMovementType.Purchase), allowNegative: true, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return TypedResults.Ok(await LoadDtoAsync(db, id, ct));
    }

    private static async Task<NoContent> Delete(int id, FatouraDbContext db, StockService stock, CancellationToken ct)
    {
        await db.InTransactionAsync(async () =>
        {
            var p = await db.PurchaseInvoices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Purchase");
            await stock.ApplyAsync(StockChanges(p, p.Lines, -1, StockMovementType.PurchaseReversal), allowNegative: true, ct);
            await db.SaveChangesAsync(ct);
            db.PurchaseInvoices.Remove(p);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> GetAttachment(int id, FatouraDbContext db, CancellationToken ct)
    {
        var a = await db.PurchaseInvoices.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.Attachment, p.AttachmentContentType, p.AttachmentFileName }).SingleOrDefaultAsync(ct);
        return a?.Attachment is null
            ? TypedResults.NotFound()
            : TypedResults.File(a.Attachment, a.AttachmentContentType ?? "application/octet-stream", a.AttachmentFileName);
    }

    private static async Task<NoContent> PutAttachment(int id, IFormFile file, FatouraDbContext db, CancellationToken ct)
    {
        if (file.Length is 0 or > MaxAttachmentBytes)
        {
            throw InvalidRequestException.For("file", "Attachment must be between 1 byte and 5 MB.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var type = ImageSniffer.Detect(bytes)
            ?? (bytes.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf" : null)
            ?? throw InvalidRequestException.For("file", "Only PDF, PNG and JPEG files are supported.");

        var p = await db.PurchaseInvoices.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Purchase");
        p.Attachment = bytes;
        p.AttachmentContentType = type;
        p.AttachmentFileName = Path.GetFileName(file.FileName);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteAttachment(int id, FatouraDbContext db, CancellationToken ct)
    {
        var p = await db.PurchaseInvoices.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Purchase");
        (p.Attachment, p.AttachmentContentType, p.AttachmentFileName) = (null, null, null);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task ValidateAsync(PurchaseRequest r, FatouraDbContext db, BusinessClock clock, CancellationToken ct)
    {
        var v = new FieldValidator().Require(r.Date <= clock.Today, "date", "The purchase date cannot be in the future.");
        await DocumentLines.ValidateAsync(r.Lines?.Select(ToLineRequest).ToList(), db, v, ct, allowInactiveItems: true);
        foreach (var (l, i) in (r.Lines ?? []).Select((l, i) => (l, i)))
        {
            v.Require((l.ExpenseCategory?.Length ?? 0) <= 100, $"lines[{i}].expenseCategory", "Category must be at most 100 characters.");
        }

        v.ThrowIfInvalid();
    }

    private static DocumentLineRequest ToLineRequest(PurchaseLineRequest l) => new(l.ItemId, l.Description, l.Quantity, l.UnitPrice, l.TaxCategory);

    private static void Apply(PurchaseInvoice p, PurchaseRequest r, Supplier supplier, decimal vatRate, DateTimeOffset now)
    {
        p.SupplierId = supplier.Id;
        p.SupplierSnapshot = PartySnapshot.From(supplier);
        p.SupplierInvoiceNo = r.SupplierInvoiceNo?.Trim() ?? string.Empty;
        p.Date = r.Date;
        p.Notes = r.Notes?.Trim() ?? string.Empty;
        var totals = DocumentLines.Build(r.Lines.Select(ToLineRequest).ToList(), vatRate, p.Lines);
        for (var i = 0; i < r.Lines.Count; i++)
        {
            p.Lines[i].ExpenseCategory = r.Lines[i].ItemId is null ? r.Lines[i].ExpenseCategory?.Trim() ?? string.Empty : string.Empty;
        }

        (p.SubTotal, p.VatTotal, p.Total) = totals;
        p.UpdatedAt = now;
    }

    private static IEnumerable<StockChange> StockChanges(PurchaseInvoice p, IEnumerable<PurchaseLine> lines, int sign, StockMovementType type) =>
        lines.Where(l => l.ItemId is not null)
            .GroupBy(l => l.ItemId!.Value)
            .Select(g =>
            {
                var qty = g.Sum(l => l.Quantity);
                var cost = qty == 0 ? 0 : Math.Round(g.Sum(l => l.Net) / qty, 4);
                return new StockChange(g.Key, sign * qty, type, p.Number, PurchaseInvoiceId: p.Id, UnitCost: sign > 0 ? cost : null);
            });
}
