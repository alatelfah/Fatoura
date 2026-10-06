namespace Fatoura.Api.Data.Entities;

public enum Emirate
{
    AbuDhabi = 0,
    Dubai = 1,
    Sharjah = 2,
    Ajman = 3,
    UmmAlQuwain = 4,
    RasAlKhaimah = 5,
    Fujairah = 6,
}

public enum ItemType
{
    Product = 0,
    Service = 1,
}

public enum QuotationStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3,
    Converted = 4,
}

public enum InvoiceStatus
{
    Issued = 0,
    Void = 1,
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    BankTransfer = 2,
    Cheque = 3,
}

public enum StockMovementType
{
    Purchase = 0,
    Sale = 1,
    CreditNote = 2,
    Void = 3,
    Adjustment = 4,
    PurchaseReversal = 5,
    SaleReversal = 6,
}
