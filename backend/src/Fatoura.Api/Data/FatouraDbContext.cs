using Fatoura.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fatoura.Api.Data;

public sealed class FatouraDbContext(DbContextOptions<FatouraDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<NumberingSetting> NumberingSettings => Set<NumberingSetting>();

    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Quotation> Quotations => Set<Quotation>();

    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();

    public DbSet<CreditNoteLine> CreditNoteLines => Set<CreditNoteLine>();

    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();

    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.Property(u => u.DisplayName).HaveMax(100);
            e.Property(u => u.PreferredLanguage).HaveMax(5);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(t => t.TokenHash).HaveMax(64);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.FamilyId);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CompanySettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Address).HaveMax(500);
            e.Property(s => s.Email).HaveMax(256);
            e.Property(s => s.Trn).HaveMax(15);
            e.Property(s => s.VatRate).HasPrecision(5, 4);
            e.Property(s => s.LogoContentType).HaveMax(50);
            e.Property(s => s.StampContentType).HaveMax(50);
            ConfigureTerms(e);
            e.Property(s => s.Emirate).HasConversion<string>().HaveMax(20);
        });

        b.Entity<NumberingSetting>(e =>
        {
            e.HasKey(n => n.DocumentType);
            e.Property(n => n.DocumentType).HasConversion<string>().HaveMax(20);
            e.Property(n => n.Reset).HasConversion<string>().HaveMax(20);
            e.Property(n => n.Pattern).HaveMax(40);
        });

        b.Entity<DocumentSequence>(e =>
        {
            e.HasKey(s => new { s.DocumentType, s.ResetKey });
            e.Property(s => s.DocumentType).HasConversion<string>().HaveMax(20);
            e.Property(s => s.ResetKey).HaveMax(10);
        });

        b.Entity<Client>(e =>
        {
            ConfigureContact(e);
            e.HasIndex(c => c.Name);
            e.HasIndex(c => c.Trn);
        });

        b.Entity<Supplier>(e =>
        {
            ConfigureContact(e);
            e.HasIndex(s => s.Name);
        });

        b.Entity<Item>(e =>
        {
            e.Property(i => i.Description).HaveMax(1000);
            e.Property(i => i.Type).HasConversion<string>().HaveMax(20);
            e.Property(i => i.TaxCategory).HasConversion<string>().HaveMax(20);
            e.Property(i => i.StockQty).HasPrecision(18, 3);
            e.Property(i => i.ReorderLevel).HasPrecision(18, 3);
            e.Property(i => i.AvgCost).HasPrecision(18, 4);
            e.HasIndex(i => i.Name);
        });

        b.Entity<StockMovement>(e =>
        {
            e.Property(m => m.Quantity).HasPrecision(18, 3);
            e.Property(m => m.Type).HasConversion<string>().HaveMax(20);
            e.Property(m => m.Reference).HaveMax(40);
            e.Property(m => m.Note).HaveMax(500);
            e.HasOne(m => m.Item).WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.ItemId, m.At });
        });

        b.Entity<Quotation>(e =>
        {
            e.Property(q => q.Number).HaveMax(40);
            e.HasIndex(q => q.Number).IsUnique();
            e.HasIndex(q => q.Date);
            e.Property(q => q.Status).HasConversion<string>().HaveMax(20);
            e.OwnsOne(q => q.ClientSnapshot, ConfigureParty);
            ConfigureTerms(e);
            e.HasOne(q => q.Client).WithMany().HasForeignKey(q => q.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.CreatedBy).WithMany().HasForeignKey(q => q.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(q => q.Lines).WithOne().HasForeignKey(l => l.QuotationId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<QuotationLine>(ConfigureLine);

        b.Entity<Invoice>(e =>
        {
            e.Property(i => i.Number).HaveMax(40);
            e.HasIndex(i => i.Number).IsUnique();
            e.HasIndex(i => i.Date);
            e.HasIndex(i => new { i.CreatedById, i.Date });
            e.Property(i => i.QuotationNumber).HaveMax(40);
            e.Property(i => i.Status).HasConversion<string>().HaveMax(20);
            e.Property(i => i.VoidReason).HaveMax(500);
            e.OwnsOne(i => i.ClientSnapshot, ConfigureParty);
            e.OwnsOne(i => i.CompanySnapshot, ConfigureCompany);
            ConfigureTerms(e);
            e.HasOne(i => i.Client).WithMany().HasForeignKey(i => i.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.CreatedBy).WithMany().HasForeignKey(i => i.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(i => i.Payments).WithOne(p => p.Invoice).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(i => i.CreditNotes).WithOne(c => c.Invoice).HasForeignKey(c => c.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<InvoiceLine>(e =>
        {
            ConfigureLine(e);
            e.Property(l => l.UnitCost).HasPrecision(18, 4);
        });

        b.Entity<Payment>(e =>
        {
            e.Property(p => p.Method).HasConversion<string>().HaveMax(20);
            e.Property(p => p.Reference).HaveMax(100);
        });

        b.Entity<CreditNote>(e =>
        {
            e.Property(c => c.Number).HaveMax(40);
            e.HasIndex(c => c.Number).IsUnique();
            e.HasIndex(c => c.Date);
            e.Property(c => c.Reason).HaveMax(500);
            e.OwnsOne(c => c.ClientSnapshot, ConfigureParty);
            e.OwnsOne(c => c.CompanySnapshot, ConfigureCompany);
            e.HasOne(c => c.CreatedBy).WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(c => c.Lines).WithOne().HasForeignKey(l => l.CreditNoteId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CreditNoteLine>(e =>
        {
            ConfigureLine(e);
            e.HasOne(l => l.InvoiceLine).WithMany().HasForeignKey(l => l.InvoiceLineId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PurchaseInvoice>(e =>
        {
            e.Property(p => p.Number).HaveMax(40);
            e.HasIndex(p => p.Number).IsUnique();
            e.HasIndex(p => p.Date);
            e.Property(p => p.SupplierInvoiceNo).HaveMax(60);
            e.Property(p => p.Notes).HaveMax(2000);
            e.Property(p => p.AttachmentContentType).HaveMax(100);
            e.Property(p => p.AttachmentFileName).HaveMax(260);
            e.OwnsOne(p => p.SupplierSnapshot, ConfigureParty);
            e.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Lines).WithOne().HasForeignKey(l => l.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PurchaseLine>(e =>
        {
            ConfigureLine(e);
            e.Property(l => l.ExpenseCategory).HaveMax(100);
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Action).HaveMax(40);
            e.Property(a => a.EntityType).HaveMax(60);
            e.Property(a => a.EntityId).HaveMax(60);
            e.Property(a => a.UserName).HaveMax(256);
            e.Property(a => a.Changes).HasColumnType("nvarchar(max)");
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.At);
        });
    }

    private static void ConfigureContact<T>(EntityTypeBuilder<T> e)
        where T : class
    {
        e.Property<string>("Name").HaveMax(200);
        e.Property<string>("Phone").HaveMax(50);
        e.Property<string>("Email").HaveMax(256);
        e.Property<string>("Address").HaveMax(500);
        e.Property<string>("Trn").HaveMax(15);
    }

    private static void ConfigureTerms<T>(EntityTypeBuilder<T> e)
        where T : class
    {
        e.Property<string>(nameof(IDocumentTerms.PaymentTerms)).HaveMax(1000);
        e.Property<string>(nameof(IDocumentTerms.CompletionOfWork)).HaveMax(1000);
        e.Property<string>(nameof(IDocumentTerms.Notes)).HaveMax(4000);
        e.Property<string>(nameof(IDocumentTerms.ClosingText)).HaveMax(1000);
    }

    private static void ConfigureLine<T>(EntityTypeBuilder<T> e)
        where T : DocumentLineBase
    {
        e.Property(l => l.Description).HaveMax(1000);
        e.Property(l => l.Quantity).HasPrecision(18, 3);
        e.Property(l => l.VatRate).HasPrecision(5, 4);
        e.Property(l => l.TaxCategory).HasConversion<string>().HaveMax(20);
    }

    private static void ConfigureParty<TOwner>(OwnedNavigationBuilder<TOwner, PartySnapshot> o)
        where TOwner : class
    {
        o.Property(p => p.Name).HaveMax(200);
        o.Property(p => p.Address).HaveMax(500);
        o.Property(p => p.Phone).HaveMax(50);
        o.Property(p => p.Email).HaveMax(256);
        o.Property(p => p.Trn).HaveMax(15);
    }

    private static void ConfigureCompany<TOwner>(OwnedNavigationBuilder<TOwner, CompanySnapshot> o)
        where TOwner : class
    {
        o.Property(p => p.Name).HaveMax(200);
        o.Property(p => p.Address).HaveMax(500);
        o.Property(p => p.Phone).HaveMax(50);
        o.Property(p => p.Email).HaveMax(256);
        o.Property(p => p.Website).HaveMax(200);
        o.Property(p => p.Trn).HaveMax(15);
    }
}

internal static class PropertyBuilderExtensions
{
    public static PropertyBuilder<T> HaveMax<T>(this PropertyBuilder<T> p, int length) => p.HasMaxLength(length);
}
