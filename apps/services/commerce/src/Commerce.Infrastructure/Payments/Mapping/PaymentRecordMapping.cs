using Commerce.Contracts.Payments;
using Commerce.Domain.Payments;

namespace Commerce.Infrastructure.Payments.Mapping;

internal static class PaymentRecordMapping
{
    public static PaymentResponse ToResponse(this Payment p) => new(
        p.Id, p.BillingAccountId, p.InvoiceId, p.SubscriptionId,
        p.Provider, p.ProviderPaymentId, p.ProviderCustomerId,
        p.AmountMinor, p.Currency, p.Status,
        p.PaidAtUtc, p.FailureCode, p.FailureMessage,
        p.CreatedAtUtc, p.UpdatedAtUtc,
        p.Method, p.Notes, p.RecordedByLabel,
        p.TransactionReference);

    public static PaymentAttemptResponse ToResponse(this PaymentAttempt a) => new(
        a.Id, a.PaymentId, a.BillingAccountId, a.SubscriptionId,
        a.Provider, a.ProviderEventId, a.AttemptedAtUtc, a.Status,
        a.ErrorCode, a.ErrorMessage, a.CreatedAtUtc);
}
