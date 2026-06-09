using Billing.Domain.Statements.Analytics;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-OPS-002 — Wire shape for the delivery summary endpoint.
/// Mirrors <see cref="DeliverySummaryRow"/> 1:1; the controller
/// does not reshape, derive, or hide any field.
/// </summary>
public sealed record DeliverySummaryResponse(
    System.DateTime WindowStartUtc,
    System.DateTime WindowEndUtc,
    int WindowDays,
    int TotalSnapshots,
    int EverAttempted,
    int Sent,
    int Failed,
    int RetryableFailure,
    int InvalidRecipient,
    int ProviderUnavailable,
    int OtherStatuses,
    double SuccessRatePercent)
{
    public static DeliverySummaryResponse From(DeliverySummaryRow r)
    {
        var days = (int)System.Math.Max(1, System.Math.Round((r.WindowEndUtc - r.WindowStartUtc).TotalDays));
        var rate = r.EverAttempted == 0
            ? 0d
            : System.Math.Round(100d * r.Sent / r.EverAttempted, 2);
        return new DeliverySummaryResponse(
            r.WindowStartUtc, r.WindowEndUtc, days,
            r.TotalSnapshots, r.EverAttempted, r.Sent, r.Failed,
            r.RetryableFailure, r.InvalidRecipient, r.ProviderUnavailable,
            r.OtherStatuses, rate);
    }
}

/// <summary>
/// MS-BILL-OPS-002 — Wire shape for the trend endpoint. One bucket
/// per UTC day in the lookback window with at least one attempt.
/// </summary>
public sealed record DeliveryTrendBucketResponse(
    System.DateTime BucketDateUtc,
    int Attempts,
    int Sent,
    int Failed,
    int RetryableFailure,
    int InvalidRecipient,
    int ProviderUnavailable)
{
    public static DeliveryTrendBucketResponse From(DeliveryTrendBucketRow r) => new(
        r.BucketDateUtc, r.Attempts, r.Sent, r.Failed,
        r.RetryableFailure, r.InvalidRecipient, r.ProviderUnavailable);
}

public sealed record DeliveryTrendResponse(
    System.DateTime WindowStartUtc,
    System.DateTime WindowEndUtc,
    int WindowDays,
    System.Collections.Generic.IReadOnlyList<DeliveryTrendBucketResponse> Buckets);

/// <summary>
/// MS-BILL-OPS-002 — Wire shape for the retry-analytics endpoint.
/// </summary>
public sealed record RetryAnalyticsResponse(
    int MaxAttemptsConfigured,
    int CooldownSecondsConfigured,
    int SnapshotsAtRetryLimit,
    int SnapshotsInCooldownNow,
    int SnapshotsWithAnyRetry,
    int TotalRetryAttemptsRecorded,
    System.Collections.Generic.IReadOnlyList<TopRetriedSnapshotResponse> TopRetried)
{
    public static RetryAnalyticsResponse From(RetryAnalyticsRow r) => new(
        r.MaxAttemptsConfigured,
        r.CooldownSecondsConfigured,
        r.SnapshotsAtRetryLimit,
        r.SnapshotsInCooldownNow,
        r.SnapshotsWithAnyRetry,
        r.TotalRetryAttemptsRecorded,
        r.TopRetried.Select(TopRetriedSnapshotResponse.From).ToList());
}

public sealed record TopRetriedSnapshotResponse(
    System.Guid StatementId,
    string StatementNumber,
    System.Guid CustomerId,
    int RetryCount,
    string? LastDeliveryStatus,
    string? LastFailureReason,
    System.DateTime? LastAttemptedAtUtc)
{
    public static TopRetriedSnapshotResponse From(TopRetriedSnapshotRow r) => new(
        r.StatementId, r.StatementNumber, r.CustomerId,
        r.RetryCount, r.LastDeliveryStatus, r.LastFailureReason,
        r.LastAttemptedAtUtc);
}

/// <summary>
/// MS-BILL-OPS-002 — Wire shape for the provider-health analytics
/// endpoint. The "live" block is the in-memory rolling-window
/// snapshot from <see cref="Billing.Domain.Statements.Delivery.IProviderHealthMonitor"/>
/// — operator visibility only. The "providers" array is the SQL
/// projection of lifetime per-provider history.
/// </summary>
public sealed record ProviderHealthAnalyticsResponse(
    string ActiveProviderName,
    string CurrentHealthState,
    int RecentFailures,
    int RecentSuccesses,
    int WindowSeconds,
    System.DateTime ObservedAtUtc,
    System.Collections.Generic.IReadOnlyList<ProviderLifetimeResponse> Providers);

public sealed record ProviderLifetimeResponse(
    string ProviderName,
    bool IsActive,
    int LifetimeSends,
    int LifetimeFailures,
    int LifetimeRetryableFailures,
    System.DateTime? LastSuccessfulSendUtc,
    System.DateTime? LastFailureUtc);
