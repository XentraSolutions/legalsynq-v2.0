using Commerce.Domain.Common;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Domain.Payments;

public sealed class PaymentAttempt : Entity<Guid>
{
    public Guid? PaymentId { get; private set; }
    public Guid BillingAccountId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public string? ProviderEventId { get; private set; }
    public DateTime AttemptedAtUtc { get; private set; }
    public PaymentAttemptStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaymentAttempt() { }

    public static PaymentAttempt Create(
        Guid? paymentId,
        Guid billingAccountId,
        Guid? subscriptionId,
        PaymentProviderType provider,
        string? providerEventId,
        DateTime attemptedAtUtc,
        PaymentAttemptStatus status,
        string? errorCode,
        string? errorMessage,
        DateTime nowUtc)
    {
        if (billingAccountId == Guid.Empty)
            throw new InvalidOperationException("BillingAccountId is required.");
        return new PaymentAttempt
        {
            Id = Guid.CreateVersion7(),
            PaymentId = paymentId,
            BillingAccountId = billingAccountId,
            SubscriptionId = subscriptionId,
            Provider = provider,
            ProviderEventId = string.IsNullOrWhiteSpace(providerEventId) ? null : providerEventId.Trim(),
            AttemptedAtUtc = attemptedAtUtc,
            Status = status,
            ErrorCode = Trim(errorCode, 64),
            ErrorMessage = Trim(errorMessage, 500),
            CreatedAtUtc = nowUtc
        };
    }

    private static string? Trim(string? raw, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
