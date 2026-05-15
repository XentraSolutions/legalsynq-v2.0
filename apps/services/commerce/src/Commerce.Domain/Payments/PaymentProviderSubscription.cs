using Commerce.Domain.Common;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Domain.Payments;

/// <summary>
/// Maps a Commerce <c>Subscription</c> to a provider subscription /
/// checkout session. Tracks only provider-safe references. The Commerce
/// subscription lifecycle is NOT updated from this row in COM-B05; that
/// is reserved for COM-B06.
/// </summary>
public sealed class PaymentProviderSubscription : Entity<Guid>
{
    public Guid SubscriptionId { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public string? ProviderSubscriptionId { get; private set; }
    public string? ProviderCheckoutSessionId { get; private set; }
    public string? ProviderCustomerId { get; private set; }
    public ProviderSubscriptionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PaymentProviderSubscription() { }

    public static PaymentProviderSubscription Create(
        Guid subscriptionId,
        PaymentProviderType provider,
        string? providerCustomerId,
        string? providerCheckoutSessionId,
        DateTime nowUtc)
    {
        if (subscriptionId == Guid.Empty)
            throw new InvalidOperationException("SubscriptionId is required.");

        return new PaymentProviderSubscription
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            Provider = provider,
            ProviderCustomerId = providerCustomerId,
            ProviderCheckoutSessionId = providerCheckoutSessionId,
            ProviderSubscriptionId = null,
            Status = ProviderSubscriptionStatus.Pending,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void AttachCheckoutSession(string sessionId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new InvalidOperationException("Checkout session id is required.");
        ProviderCheckoutSessionId = sessionId.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void MarkActive(string? providerSubscriptionId, DateTime nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(providerSubscriptionId))
            ProviderSubscriptionId = providerSubscriptionId.Trim();
        Status = ProviderSubscriptionStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCancelled(DateTime nowUtc)
    {
        Status = ProviderSubscriptionStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFailed(DateTime nowUtc)
    {
        Status = ProviderSubscriptionStatus.Failed;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkUnknown(DateTime nowUtc)
    {
        Status = ProviderSubscriptionStatus.Unknown;
        UpdatedAtUtc = nowUtc;
    }
}
