namespace Commerce.Domain.Invoicing.Enums;

public enum InvoiceStatus
{
    Draft = 0,
    Open = 1,
    Paid = 2,
    Void = 3,
    Uncollectible = 4
}
