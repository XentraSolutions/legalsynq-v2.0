using Commerce.Domain.Payments.Enums;

namespace Commerce.Application.Payments.Abstractions;

// ----- Provider request / response models (POCOs only — no SDK types)

public sealed record ProviderCustomerRequest(
    Guid BillingAccountId,
    string? Email,
    string? Name,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ProviderCustomerResult(
    string ProviderCustomerId,
    string? Email,
    string? Name);

public sealed record ProviderCheckoutLineItem(
    string ProviderPriceId,
    int Quantity);

public sealed record ProviderCheckoutRequest(
    Guid BillingAccountId,
    Guid SubscriptionId,
    string ProviderCustomerId,
    string SuccessUrl,
    string CancelUrl,
    IReadOnlyList<ProviderCheckoutLineItem> LineItems,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ProviderCheckoutResult(
    string CheckoutSessionId,
    string CheckoutUrl,
    string? ProviderSubscriptionId,
    DateTime? ExpiresAtUtc);

public sealed record ProviderWebhookPayload(
    string RawBody,
    string SignatureHeader);

/// <summary>
/// Provider-agnostic projection of a webhook event. The Stripe adapter
/// translates Stripe payloads into this. Application services react
/// only to this normalized shape — they never see SDK types.
/// </summary>
public sealed record NormalizedProviderEvent(
    PaymentProviderType Provider,
    string ProviderEventId,
    string EventType,
    NormalizedProviderEventKind Kind,
    string? ProviderCustomerId,
    string? ProviderSubscriptionId,
    string? ProviderCheckoutSessionId,
    string? ProviderPaymentMethodId,
    string? PaymentMethodBrand,
    string? PaymentMethodLast4,
    int? PaymentMethodExpMonth,
    int? PaymentMethodExpYear,
    Guid? BillingAccountId,
    Guid? SubscriptionId,
    // COM-B06 — financial fields used by payment-record + invoice mapping.
    string? ProviderPaymentIntentId = null,
    string? ProviderInvoiceId = null,
    long? AmountMinor = null,
    string? Currency = null,
    string? FailureCode = null,
    string? FailureMessage = null,
    DateTime? OccurredAtUtc = null,
    ProviderSubscriptionStatus? ProviderSubscriptionStatus = null);

public enum NormalizedProviderEventKind
{
    Unsupported = 0,
    CheckoutSessionCompleted = 1,
    CheckoutSessionExpired = 2,
    SubscriptionCreated = 3,
    SubscriptionUpdated = 4,
    SubscriptionDeleted = 5,
    PaymentMethodAttached = 6,
    // COM-B06 — payment-related events.
    PaymentIntentSucceeded = 7,
    PaymentIntentFailed = 8,
    InvoicePaymentSucceeded = 9,
    InvoicePaymentFailed = 10
}

/// <summary>
/// Adapter contract for one payment provider. Implementations live in
/// Infrastructure and may use SDK or HTTP calls internally.
/// </summary>
public interface IPaymentProvider
{
    PaymentProviderType ProviderType { get; }
    bool IsEnabled { get; }

    Task<ProviderCustomerResult> CreateOrGetCustomerAsync(
        ProviderCustomerRequest request, CancellationToken ct);

    Task<ProviderCheckoutResult> CreateCheckoutSessionAsync(
        ProviderCheckoutRequest request, CancellationToken ct);

    /// <summary>Throws <c>InvalidWebhookSignatureException</c> on failure.</summary>
    void VerifyWebhook(ProviderWebhookPayload payload);

    NormalizedProviderEvent TranslateWebhookEvent(string rawBody);
}

public interface IPaymentProviderRegistry
{
    IPaymentProvider Get(PaymentProviderType type);
    bool TryGet(PaymentProviderType type, out IPaymentProvider provider);
}
