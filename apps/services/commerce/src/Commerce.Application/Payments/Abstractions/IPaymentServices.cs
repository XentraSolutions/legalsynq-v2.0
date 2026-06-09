using Commerce.Contracts.Payments;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Application.Payments.Abstractions;

public interface IPaymentProviderCustomerService
{
    Task<PaymentProviderCustomerResponse> CreateOrGetAsync(
        Guid billingAccountId,
        PaymentProviderType provider,
        string? email,
        string? name,
        CancellationToken ct);

    Task<IReadOnlyList<PaymentProviderCustomerResponse>> ListForAccountAsync(
        Guid billingAccountId, CancellationToken ct);
}

public interface IPaymentCheckoutService
{
    Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct);
}

public sealed record WebhookProcessingResult(
    Guid EventLogId,
    PaymentProviderEventProcessingStatus Status,
    string? Reason);

public interface IPaymentWebhookService
{
    /// <summary>
    /// Receives a raw provider webhook, verifies the signature, persists
    /// the event log, applies idempotent state changes, and returns the
    /// final outcome. Throws <c>InvalidWebhookSignatureException</c> on
    /// signature failure (mapped to 400). Duplicate events are not an
    /// error: they return a <c>Duplicate</c> outcome.
    /// </summary>
    Task<WebhookProcessingResult> ReceiveAsync(
        PaymentProviderType provider,
        string rawBody,
        string signatureHeader,
        CancellationToken ct);

    Task<IReadOnlyList<PaymentProviderEventLogResponse>> ListAsync(
        PaymentProviderType? provider,
        PaymentProviderEventProcessingStatus? status,
        int take,
        CancellationToken ct);

    Task<PaymentProviderEventLogResponse> GetAsync(Guid id, CancellationToken ct);
}

public interface IPaymentMethodReferenceService
{
    Task<IReadOnlyList<PaymentMethodReferenceResponse>> ListForAccountAsync(
        Guid billingAccountId, CancellationToken ct);

    Task<PaymentMethodReferenceResponse> MakeDefaultAsync(
        Guid billingAccountId, Guid paymentMethodId, CancellationToken ct);
}
