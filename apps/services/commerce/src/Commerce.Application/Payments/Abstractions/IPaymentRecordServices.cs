using Commerce.Contracts.Payments;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Application.Payments.Abstractions;

public interface IPaymentRecordService
{
    Task<PaymentResponse> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PaymentResponse>> ListAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<PaymentResponse>> ListForBillingAccountAsync(Guid billingAccountId, CancellationToken ct);
    Task<IReadOnlyList<PaymentResponse>> ListForSubscriptionAsync(Guid subscriptionId, CancellationToken ct);

    Task<PaymentAttemptResponse> GetAttemptAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PaymentAttemptResponse>> ListAttemptsAsync(int take, CancellationToken ct);
}

public sealed record PaymentRecordingResult(
    Guid PaymentId,
    Guid AttemptId,
    PaymentStatus PaymentStatus,
    bool MatchedInvoice);

/// <summary>
/// Internal contract used by <c>PaymentWebhookService</c> to record a
/// Commerce-owned <c>Payment</c> + <c>PaymentAttempt</c> from a
/// translated provider event. Idempotent on
/// <c>(Provider, ProviderEventId)</c>.
/// </summary>
public interface IPaymentRecordingService
{
    Task<PaymentRecordingResult?> RecordFromEventAsync(
        NormalizedProviderEvent ev,
        bool succeeded,
        CancellationToken ct);
}

/// <summary>
/// Internal contract used by <c>PaymentWebhookService</c> to reconcile
/// a Commerce <c>Subscription</c> from the latest provider signal. Writes
/// <c>SubscriptionChange</c> rows when the local subscription changes
/// state.
/// </summary>
public interface ISubscriptionReconciliationService
{
    Task<bool> ReconcileFromEventAsync(NormalizedProviderEvent ev, CancellationToken ct);
}

public interface IProviderEventReplayService
{
    Task<ReprocessProviderEventResponse> ReprocessAsync(Guid eventLogId, CancellationToken ct);
}

/// <summary>
/// Records an out-of-band ("manual") payment against an existing
/// invoice. The resulting <c>Payment</c> is persisted with
/// <see cref="PaymentProviderType.Manual"/>, status
/// <see cref="PaymentStatus.Succeeded"/>, and is applied to the invoice
/// via <c>Invoice.RegisterPayment</c> in the same unit of work.
/// </summary>
public interface IManualPaymentRecordingService
{
    Task<PaymentResponse> RecordAsync(
        Guid invoiceId,
        RecordManualPaymentRequest request,
        CancellationToken ct);
}
