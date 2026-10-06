namespace Fatoura.Domain.Numbering;

public enum DocumentType
{
    Invoice = 0,
    Quotation = 1,
    CreditNote = 2,
    Purchase = 3,
}

public enum SequenceReset
{
    Never = 0,
    Yearly = 1,
    Monthly = 2,
}
