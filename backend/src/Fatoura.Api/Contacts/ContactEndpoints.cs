using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Domain.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Contacts;

public sealed record ClientDto(int Id, string Name, string Phone, string Email, string Address, string Trn, bool IsActive, DateTimeOffset CreatedAt);

public sealed record SupplierDto(int Id, string Name, string Phone, string Email, string Address, string Trn, bool IsActive, DateTimeOffset CreatedAt);

public sealed record ContactRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: MaxLength(50)] string? Phone,
    [property: MaxLength(256)] string? Email,
    [property: MaxLength(500)] string? Address,
    [property: MaxLength(30)] string? Trn,
    bool IsActive = true);

public static class ContactEndpoints
{
    public static RouteGroupBuilder MapClientEndpoints(this RouteGroupBuilder api)
    {
        // BRD §2: cashiers can view and add clients; editing and deleting is Admin only.
        var group = api.MapGroup("/clients").WithTags("Clients");
        group.MapGet("/", ListClients).RequireAuthorization(Policies.Staff);
        group.MapGet("/{id:int}", GetClient).RequireAuthorization(Policies.Staff);
        group.MapPost("/", CreateClient).RequireAuthorization(Policies.Staff);
        group.MapPut("/{id:int}", UpdateClient).RequireAuthorization(Policies.Admin);
        group.MapDelete("/{id:int}", DeleteClient).RequireAuthorization(Policies.Admin);
        return group;
    }

    public static RouteGroupBuilder MapSupplierEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/suppliers").WithTags("Suppliers").RequireAuthorization(Policies.Admin);
        group.MapGet("/", ListSuppliers);
        group.MapGet("/{id:int}", GetSupplier);
        group.MapPost("/", CreateSupplier);
        group.MapPut("/{id:int}", UpdateSupplier);
        group.MapDelete("/{id:int}", DeleteSupplier);
        return group;
    }

    private static ClientDto ToDto(Client c) => new(c.Id, c.Name, c.Phone, c.Email, c.Address, c.Trn, c.IsActive, c.CreatedAt);

    private static SupplierDto ToDto(Supplier s) => new(s.Id, s.Name, s.Phone, s.Email, s.Address, s.Trn, s.IsActive, s.CreatedAt);

    private static async Task<Ok<PagedResult<ClientDto>>> ListClients(
        FatouraDbContext db, string? search, bool? includeInactive, int? page, int? pageSize, CancellationToken ct)
    {
        var q = db.Clients.AsNoTracking();
        if (includeInactive != true)
        {
            q = q.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = Paging.Like(search);
            q = q.Where(c => EF.Functions.Like(c.Name, like) || EF.Functions.Like(c.Phone, like)
                || EF.Functions.Like(c.Email, like) || EF.Functions.Like(c.Trn, like));
        }

        var result = await q.OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new ClientDto(c.Id, c.Name, c.Phone, c.Email, c.Address, c.Trn, c.IsActive, c.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ClientDto>, NotFound>> GetClient(int id, FatouraDbContext db, CancellationToken ct) =>
        await db.Clients.FindAsync([id], ct) is { } c ? TypedResults.Ok(ToDto(c)) : TypedResults.NotFound();

    private static async Task<Created<ClientDto>> CreateClient(ContactRequest r, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        var c = new Client { CreatedAt = time.GetUtcNow() };
        Apply(r, c);
        c.UpdatedAt = c.CreatedAt;
        db.Clients.Add(c);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/clients/{c.Id}", ToDto(c));
    }

    private static async Task<Results<Ok<ClientDto>, NotFound>> UpdateClient(
        int id, ContactRequest r, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Clients.FindAsync([id], ct) is not { } c)
        {
            return TypedResults.NotFound();
        }

        Apply(r, c);
        c.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(c));
    }

    /// <summary>Deletes an unused client; a client with documents is deactivated instead (documents keep their snapshot).</summary>
    private static async Task<Results<NoContent, NotFound>> DeleteClient(int id, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Clients.FindAsync([id], ct) is not { } c)
        {
            return TypedResults.NotFound();
        }

        var used = await db.Invoices.AnyAsync(i => i.ClientId == id, ct) || await db.Quotations.AnyAsync(q => q.ClientId == id, ct);
        if (used)
        {
            c.IsActive = false;
            c.UpdatedAt = time.GetUtcNow();
        }
        else
        {
            db.Clients.Remove(c);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<PagedResult<SupplierDto>>> ListSuppliers(
        FatouraDbContext db, string? search, bool? includeInactive, int? page, int? pageSize, CancellationToken ct)
    {
        var q = db.Suppliers.AsNoTracking();
        if (includeInactive != true)
        {
            q = q.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = Paging.Like(search);
            q = q.Where(s => EF.Functions.Like(s.Name, like) || EF.Functions.Like(s.Phone, like) || EF.Functions.Like(s.Trn, like));
        }

        var result = await q.OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Address, s.Trn, s.IsActive, s.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<SupplierDto>, NotFound>> GetSupplier(int id, FatouraDbContext db, CancellationToken ct) =>
        await db.Suppliers.FindAsync([id], ct) is { } s ? TypedResults.Ok(ToDto(s)) : TypedResults.NotFound();

    private static async Task<Created<SupplierDto>> CreateSupplier(ContactRequest r, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        var s = new Supplier { CreatedAt = time.GetUtcNow() };
        Apply(r, s);
        s.UpdatedAt = s.CreatedAt;
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/suppliers/{s.Id}", ToDto(s));
    }

    private static async Task<Results<Ok<SupplierDto>, NotFound>> UpdateSupplier(
        int id, ContactRequest r, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Suppliers.FindAsync([id], ct) is not { } s)
        {
            return TypedResults.NotFound();
        }

        Apply(r, s);
        s.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(s));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSupplier(int id, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Suppliers.FindAsync([id], ct) is not { } s)
        {
            return TypedResults.NotFound();
        }

        if (await db.PurchaseInvoices.AnyAsync(p => p.SupplierId == id, ct))
        {
            s.IsActive = false;
            s.UpdatedAt = time.GetUtcNow();
        }
        else
        {
            db.Suppliers.Remove(s);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static void Apply(ContactRequest r, Client c)
    {
        Validate(r).ThrowIfInvalid();
        c.Name = r.Name.Trim();
        c.Phone = r.Phone?.Trim() ?? string.Empty;
        c.Email = r.Email?.Trim() ?? string.Empty;
        c.Address = r.Address?.Trim() ?? string.Empty;
        c.Trn = TrnValidator.Normalize(r.Trn);
        c.IsActive = r.IsActive;
    }

    private static void Apply(ContactRequest r, Supplier s)
    {
        Validate(r).ThrowIfInvalid();
        s.Name = r.Name.Trim();
        s.Phone = r.Phone?.Trim() ?? string.Empty;
        s.Email = r.Email?.Trim() ?? string.Empty;
        s.Address = r.Address?.Trim() ?? string.Empty;
        s.Trn = TrnValidator.Normalize(r.Trn);
        s.IsActive = r.IsActive;
    }

    /// <summary>TRN is optional (unregistered customers) but must be valid when given.</summary>
    private static FieldValidator Validate(ContactRequest r) => new FieldValidator()
        .Require(!string.IsNullOrWhiteSpace(r.Name), "name", "Name is required.")
        .Require(string.IsNullOrWhiteSpace(r.Trn) || TrnValidator.IsValid(r.Trn), "trn", "TRN must be 15 digits starting with 10.")
        .Require(string.IsNullOrWhiteSpace(r.Email) || new EmailAddressAttribute().IsValid(r.Email.Trim()), "email", "Email is not valid.");
}
