namespace Billing.Domain.Statements.Analytics;

/// <summary>
/// MS-BILL-OPS-002 — Read-only operational analytics over the
/// existing <c>CustomerStatement</c> delivery columns
/// (<c>DeliveryStatus</c>, <c>DeliveryRetryCount</c>,
/// <c>DeliveryAttemptedAtUtc</c>, <c>DeliveryLastSentAtUtc</c>,
/// <c>DeliveryProvider</c>, <c>DeliveryFailureReason</c>) plus the
/// in-memory <c>IProviderHealthMonitor</c>. NO new persistence,
/// NO mutation, NO streaming. Models live in Billing.Domain so
/// they are pure data records and can be referenced by the
/// repository contract without an EF dependency.
/// </summary>
internal static class DeliveryAnalyticsMarker { }

/// <summary>
/// Aggregate counts over a tenant's persisted statement snapshots
/// for a bounded lookback window. The "ever attempted" count
/// excludes snapshots whose <c>DeliveryStatus</c> is null
/// (generated but never sent). Voided snapshots are still
/// included — their last-known delivery state is part of the
/// operational record.
/// </summary>
public sealed record DeliverySummaryRow(
    System.DateTime WindowStartUtc,
    System.DateTime WindowEndUtc,
    int TotalSnapshots,
    int EverAttempted,
    int Sent,
    int Failed,
    int RetryableFailure,
    int InvalidRecipient,
    int ProviderUnavailable,
    int OtherStatuses);

/// <summary>
/// Bucketed retry / cooldown analytics. Cooldown rejections and
/// retry-limit rejections are NOT persisted on the row (they
/// short-circuit before <c>RecordDeliveryAttemptAsync</c>); we
/// derive a deterministic "cap reached" projection from the
/// existing <c>DeliveryRetryCount</c> column compared to the
/// current <see cref="Billing.Domain.Statements.Delivery.StatementRetryOptions.MaxAttempts"/>
/// reported by the controller. Cooldown frequency is intentionally
/// reported as "snapshots currently inside the cooldown window"
/// — the same evaluator the orchestrator uses, applied to the
/// snapshot's <c>DeliveryAttemptedAtUtc</c> + the current options.
/// </summary>
public sealed record RetryAnalyticsRow(
    int MaxAttemptsConfigured,
    int CooldownSecondsConfigured,
    int SnapshotsAtRetryLimit,
    int SnapshotsInCooldownNow,
    int SnapshotsWithAnyRetry,
    int TotalRetryAttemptsRecorded,
    System.Collections.Generic.IReadOnlyList<TopRetriedSnapshotRow> TopRetried);

/// <summary>
/// MS-BILL-OPS-002 — Per-snapshot retry hot-spot row. Limited
/// to a small operator-facing top-N (default 10). Only fields
/// the operator needs to find the row in the snapshot history;
/// no recipient email, no rendered HTML, no provider secret.
/// </summary>
public sealed record TopRetriedSnapshotRow(
    System.Guid StatementId,
    string StatementNumber,
    System.Guid CustomerId,
    int RetryCount,
    string? LastDeliveryStatus,
    string? LastFailureReason,
    System.DateTime? LastAttemptedAtUtc);

/// <summary>
/// MS-BILL-OPS-002 — Provider-health analytics row. Combines the
/// in-memory rolling-window <see cref="Billing.Domain.Statements.Delivery.ProviderHealthSnapshot"/>
/// (operator visibility — process-local) with deterministic SQL
/// projections over the persisted delivery columns
/// (<c>LastSuccessfulSendUtc</c>, <c>LastFailureUtc</c>) so a fresh
/// process restart still surfaces useful history.
/// </summary>
public sealed record ProviderHealthAnalyticsRow(
    string ProviderName,
    string CurrentHealthState,
    int RecentFailures,
    int RecentSuccesses,
    int WindowSeconds,
    System.DateTime ObservedAtUtc,
    System.DateTime? LastSuccessfulSendUtc,
    System.DateTime? LastFailureUtc,
    int LifetimeSends,
    int LifetimeFailures,
    int LifetimeRetryableFailures);

/// <summary>
/// MS-BILL-OPS-002 — Daily trend bucket over the lookback window.
/// Bucketing is by <c>DeliveryAttemptedAtUtc</c> (UTC date) so the
/// numbers reconcile with the existing snapshot detail page. A
/// snapshot that has never been attempted contributes to no
/// bucket. The bucket date is the UTC calendar date — TZ-aware
/// presentation is the UI's responsibility.
/// </summary>
public sealed record DeliveryTrendBucketRow(
    System.DateTime BucketDateUtc,
    int Attempts,
    int Sent,
    int Failed,
    int RetryableFailure,
    int InvalidRecipient,
    int ProviderUnavailable);
