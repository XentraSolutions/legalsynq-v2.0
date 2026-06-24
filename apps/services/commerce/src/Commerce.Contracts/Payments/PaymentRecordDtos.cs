using Commerce.Domain.Payments.Enums;

namespace Commerce.Contracts.Payments;

public sealed record PaymentResponse(
    Guid Id,
    Guid BillingAccountId,
    Guid? InvoiceId,
    Guid? SubscriptionId,
    PaymentProviderType Provider,
    string? ProviderPaymentId,
    string? ProviderCustomerId,
    long AmountMinor,
    string Currency,
    PaymentStatus Status,
    DateTime? PaidAtUtc,
    string? FailureCode,
    string? FailureMessage,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? Method = null,
    string? Notes = null,
    string? RecordedByLabel = null,
    string? TransactionReference = null);

/// <summary>
/// Body for <c>POST /api/commerce/invoices/{invoiceId}/manual-payments</c>.
/// Records an out-of-band payment against the invoice (cash, check,
/// ACH, wire, etc.). The currency is derived from the invoice; the
/// caller supplies the wall-clock <see cref="PaidAtUtc"/> separately
/// from the persistence-time clock so back-dated receipts work.
/// </summary>
public sealed record RecordManualPaymentRequest(
    long AmountMinor,
    DateTime PaidAtUtc,
    string? Method = null,
    string? TransactionReference = null,
    string? RecordedByLabel = null,
    string? Notes = null);

public sealed record PaymentAttemptResponse(
    Guid Id,
    Guid? PaymentId,
    Guid BillingAccountId,
    Guid? SubscriptionId,
    PaymentProviderType Provider,
    string? ProviderEventId,
    DateTime AttemptedAtUtc,
    PaymentAttemptStatus Status,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime CreatedAtUtc);

public sealed record ReprocessProviderEventResponse(
    Guid EventLogId,
    PaymentProviderEventProcessingStatus Status,
    string? Reason);
