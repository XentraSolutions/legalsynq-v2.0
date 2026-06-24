using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Contracts.Invoicing;

public sealed record CreateInvoiceLineRequest(
    string Description,
    int Quantity,
    long UnitAmountMinor,
    Guid? SubscriptionItemId = null,
    DateTime? ServicePeriodStartUtc = null,
    DateTime? ServicePeriodEndUtc = null);

public sealed record CreateInvoiceRequest(
    Guid BillingAccountId,
    string Currency,
    IReadOnlyList<CreateInvoiceLineRequest> Lines,
    Guid? SubscriptionId = null,
    DateTime? DueDateUtc = null);

public sealed record InvoiceLineResponse(
    Guid Id,
    Guid InvoiceId,
    Guid? SubscriptionItemId,
    string Description,
    int Quantity,
    long UnitAmountMinor,
    long LineAmountMinor,
    string Currency,
    DateTime? ServicePeriodStartUtc,
    DateTime? ServicePeriodEndUtc,
    DateTime CreatedAtUtc);

public sealed record InvoiceResponse(
    Guid Id,
    Guid BillingAccountId,
    Guid? SubscriptionId,
    string InvoiceNumber,
    InvoiceStatus Status,
    string Currency,
    long SubtotalAmountMinor,
    long DiscountAmountMinor,
    long TaxAmountMinor,
    long TotalAmountMinor,
    long AmountPaidMinor,
    long AmountDueMinor,
    DateTime IssueDateUtc,
    DateTime? DueDateUtc,
    DateTime? PaidAtUtc,
    DateTime? VoidedAtUtc,
    PaymentProviderType? Provider,
    string? ProviderInvoiceId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<InvoiceLineResponse> Lines);
