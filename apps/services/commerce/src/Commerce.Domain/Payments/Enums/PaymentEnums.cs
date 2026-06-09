namespace Commerce.Domain.Payments.Enums;

/// <summary>
/// Concrete payment providers supported by Commerce. New providers are
/// added behind <c>IPaymentProvider</c> and registered with the
/// provider registry; never coupled to controllers or services.
/// </summary>
public enum PaymentProviderType
{
    Stripe = 0,

    /// <summary>
    /// Payment recorded manually by an admin (cash, check, ACH, wire,
    /// etc.). No provider webhook is involved; the admin asserts the
    /// payment was received out-of-band and supplies a method tag and
    /// optional reference / notes for audit.
    /// </summary>
    Manual = 1
}

/// <summary>
/// Lifecycle of a Commerce-tracked provider subscription mapping. This
/// is intentionally separate from <c>SubscriptionStatus</c> — the
/// Commerce subscription is the authoritative lifecycle, the provider
/// mapping reflects the latest signal from the provider.
/// </summary>
public enum ProviderSubscriptionStatus
{
    Pending = 0,
    Active = 1,
    Cancelled = 2,
    Failed = 3,
    Unknown = 4
}

/// <summary>
/// Outcome of a single provider event log row.
/// </summary>
public enum PaymentProviderEventProcessingStatus
{
    Received = 0,
    Processed = 1,
    Duplicate = 2,
    Failed = 3,
    Ignored = 4
}
