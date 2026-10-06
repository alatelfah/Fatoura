using Fatoura.Domain.Numbering;

namespace Fatoura.Api.Data.Entities;

/// <summary>Single-row table (Id = 1) holding the issuing company's profile and document defaults.</summary>
public sealed class CompanySettings : IAudited
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public Emirate Emirate { get; set; } = Emirate.Dubai;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;

    public string Trn { get; set; } = string.Empty;

    public decimal VatRate { get; set; } = 0.05m;

    public byte[]? Logo { get; set; }

    public string? LogoContentType { get; set; }

    public byte[]? Stamp { get; set; }

    public string? StampContentType { get; set; }

    public string PaymentTerms { get; set; } = string.Empty;

    public string CompletionOfWork { get; set; } = string.Empty;

    /// <summary>One note per line; printed as a numbered list.</summary>
    public string Notes { get; set; } = string.Empty;

    public string ClosingText { get; set; } = string.Empty;

    /// <summary>When true an invoice may take stock below zero (with a warning); when false it is refused.</summary>
    public bool AllowNegativeStock { get; set; } = true;

    public int QuotationValidityDays { get; set; } = 30;

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NumberingSetting : IAudited
{
    public DocumentType DocumentType { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public SequenceReset Reset { get; set; } = SequenceReset.Yearly;
}

/// <summary>Gap-free counter per document type and reset period. See SequenceService.</summary>
public sealed class DocumentSequence
{
    public DocumentType DocumentType { get; set; }

    public string ResetKey { get; set; } = "-";

    public long NextValue { get; set; } = 1;
}
