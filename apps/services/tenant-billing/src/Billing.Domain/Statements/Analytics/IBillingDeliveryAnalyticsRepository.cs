namespace Billing.Domain.Statements.Analytics;

/// <summary>
/// MS-BILL-OPS-002 — Read-only analytics projection over the
/// existing <c>CustomerStatement</c> delivery columns. Every
/// method is tenant-scoped at the SQL level (no in-memory cross-
/// tenant filtering) and pure read (<c>AsNoTracking</c>). NO
/// method on this contract may mutate state, enqueue work,
/// trigger a delivery, or expose recipient PII / provider
/// secrets.
/// </summary>
public interface IBillingDeliveryAnalyticsRepository
{
    /// <summary>
    /// Aggregate snapshot counts for the lookback window
    /// <paramref name="fromUtc"/> .. <paramref name="toUtc"/>.
    /// Counts are over <c>DeliveryAttemptedAtUtc</c>, EXCEPT the
    /// total-snapshots count which is over <c>GeneratedAtUtc</c>
    /// so generators-without-sends still surface as visible work.
    /// </summary>
    System.Threading.Tasks.Task<DeliverySummaryRow> GetSummaryAsync(
        System.Guid tenantId,
        System.DateTime fromUtc,
        System.DateTime toUtc,
        System.Threading.CancellationToken ct = default);

    /// <summary>
    /// Daily UTC-bucketed trend over the lookback window. At most
    /// <paramref name="maxBuckets"/> rows (the controller clamps to
    /// a sensible operator-visible cap — typically 30/90).
    /// </summary>
    System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<DeliveryTrendBucketRow>> GetTrendAsync(
        System.Guid tenantId,
        System.DateTime fromUtc,
        System.DateTime toUtc,
        int maxBuckets,
        System.Threading.CancellationToken ct = default);

    /// <summary>
    /// SQL-derived retry analytics: snapshots at the retry-limit,
    /// snapshots currently inside the cooldown window, and the
    /// top-N most-retried snapshots. The configured
    /// <paramref name="maxAttempts"/> and
    /// <paramref name="cooldownSeconds"/> are passed in so the
    /// repository never re-binds <c>StatementRetryOptions</c>
    /// itself (single source of truth — the controller).
    /// </summary>
    System.Threading.Tasks.Task<RetryAnalyticsRow> GetRetryAnalyticsAsync(
        System.Guid tenantId,
        int maxAttempts,
        int cooldownSeconds,
        System.DateTime nowUtc,
        int topN,
        System.Threading.CancellationToken ct = default);

    /// <summary>
    /// SQL-derived provider-history analytics for a tenant.
    /// Returns one row per <c>DeliveryProvider</c> ever recorded
    /// against this tenant's snapshots. The repository does NOT
    /// know about the in-memory health monitor; the controller
    /// merges this with the live <c>ProviderHealthSnapshot</c>
    /// before returning.
    /// </summary>
    System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<ProviderLifetimeRow>> GetProviderLifetimeAsync(
        System.Guid tenantId,
        System.Threading.CancellationToken ct = default);
}

/// <summary>
/// MS-BILL-OPS-002 — Internal projection used to build
/// <see cref="ProviderHealthAnalyticsRow"/>. The repository
/// returns these so the controller can attach the live in-memory
/// health snapshot for the active provider only.
/// </summary>
public sealed record ProviderLifetimeRow(
    string ProviderName,
    int LifetimeSends,
    int LifetimeFailures,
    int LifetimeRetryableFailures,
    System.DateTime? LastSuccessfulSendUtc,
    System.DateTime? LastFailureUtc);
