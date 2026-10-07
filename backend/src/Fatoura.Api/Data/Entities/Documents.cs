using Fatoura.Domain.Documents;
using Fatoura.Domain.Tax;

namespace Fatoura.Api.Data.Entities;

/// <summary>Amounts shared by every document line. Net/Vat/Total come from DocumentCalculator.</summary>
public abstract class DocumentLineBase
{
    public int Id { get; set; }

    public int LineNo { get; set; }

    public int? ItemId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public TaxCategory TaxCategory { get; set; }

    public decimal VatRate { get; set; }

    /// <summary>This line's share of the document discount; <see cref="Net"/> is after it.</summary>
    public decimal Discount { get; set; }

    public decimal Net { get; set; }

    public decimal Vat { get; set; }

    public decimal Total { get; set; }
}

public interface IDocumentTerms
{
    string PaymentTerms { get; set; }

    string CompletionOfWork { get; set; }

    string Notes { get; set; }

    string ClosingText { get; set; }
}

/// <summary>
/// A document-level discount as entered (<see cref="DiscountKind"/>, <see cref="DiscountValue"/>) and its amount in
/// money (<see cref="Discount"/>). <c>SubTotal</c> is after the discount; the gross is <c>SubTotal + Discount</c>.
/// </summary>
public interface IDiscounted
{
    DiscountKind DiscountKind { get; set; }

    decimal DiscountValue { get; set; }

    decimal Discount { get; set; }

    decimal SubTotal { get; set; }

    decimal VatTotal { get; set; }

    decimal Total { get; set; }
}

public sealed class Quotation : IAudited, IDocumentTerms, IDiscounted
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public DateOnly ValidUntil { get; set; }

    public int ClientId { get; set; }

    public Client? Client { get; set; }

    public PartySnapshot ClientSnapshot { get; set; } = new();

    public QuotationStatus Status { get; set; }

    public int? ConvertedInvoiceId { get; set; }

    public string PaymentTerms { get; set; } = string.Empty;

    public string CompletionOfWork { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string ClosingText { get; set; } = string.Empty;

    public DiscountKind DiscountKind { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal Discount { get; set; }

    public decimal SubTotal { get; set; }

    public decimal VatTotal { get; set; }

    public decimal Total { get; set; }

    public Guid CreatedById { get; set; }

    public AppUser? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<QuotationLine> Lines { get; set; } = [];
}

public sealed class QuotationLine : DocumentLineBase
{
    public int QuotationId { get; set; }
}

public sealed class Invoice : IAudited, IDocumentTerms, IDiscounted
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int ClientId { get; set; }

    public Client? Client { get; set; }

    public PartySnapshot ClientSnapshot { get; set; } = new();

    public CompanySnapshot CompanySnapshot { get; set; } = new();

    public int? QuotationId { get; set; }

    public string QuotationNumber { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; }

    public string VoidReason { get; set; } = string.Empty;

    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedById { get; set; }

    public string PaymentTerms { get; set; } = string.Empty;

    public string CompletionOfWork { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string ClosingText { get; set; } = string.Empty;

    public DiscountKind DiscountKind { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal Discount { get; set; }

    public decimal SubTotal { get; set; }

    public decimal VatTotal { get; set; }

    public decimal Total { get; set; }

    public Guid CreatedById { get; set; }

    public AppUser? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];

    public List<Payment> Payments { get; set; } = [];

    public List<CreditNote> CreditNotes { get; set; } = [];
}

public sealed class InvoiceLine : DocumentLineBase
{
    public int InvoiceId { get; set; }

    /// <summary>Item's moving-average cost when sold (excl. VAT); enables a COGS-based P&amp;L later.</summary>
    public decimal UnitCost { get; set; }
}

public sealed class Payment : IAudited
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public string Reference { get; set; } = string.Empty;

    public Guid CreatedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CreditNote : IAudited
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public string Reason { get; set; } = string.Empty;

    public bool ReturnToStock { get; set; }

    public PartySnapshot ClientSnapshot { get; set; } = new();

    public CompanySnapshot CompanySnapshot { get; set; } = new();

    /// <summary>The credited lines' share of the invoice discount; <c>SubTotal</c> is after it.</summary>
    public decimal Discount { get; set; }

    public decimal SubTotal { get; set; }

    public decimal VatTotal { get; set; }

    public decimal Total { get; set; }

    public Guid CreatedById { get; set; }

    public AppUser? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<CreditNoteLine> Lines { get; set; } = [];
}

public sealed class CreditNoteLine : DocumentLineBase
{
    public int CreditNoteId { get; set; }

    public int InvoiceLineId { get; set; }

    public InvoiceLine? InvoiceLine { get; set; }
}

public sealed class PurchaseInvoice : IAudited
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public string SupplierInvoiceNo { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public PartySnapshot SupplierSnapshot { get; set; } = new();

    public string Notes { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }

    public decimal VatTotal { get; set; }

    public decimal Total { get; set; }

    public byte[]? Attachment { get; set; }

    public string? AttachmentContentType { get; set; }

    public string? AttachmentFileName { get; set; }

    public Guid CreatedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<PurchaseLine> Lines { get; set; } = [];
}

/// <summary>A purchase line is either an item (adds stock) or an expense with a free-text category.</summary>
public sealed class PurchaseLine : DocumentLineBase
{
    public int PurchaseInvoiceId { get; set; }

    public string ExpenseCategory { get; set; } = string.Empty;
}
