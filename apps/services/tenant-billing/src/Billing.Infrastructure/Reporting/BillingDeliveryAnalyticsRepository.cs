using Microsoft.EntityFrameworkCore;
using Billing.Domain.Statements.Analytics;
using Billing.Domain.Statements.Delivery;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Reporting;

/// <summary>
/// MS-BILL-OPS-002 — EF Core implementation of
/// <see cref="IBillingDeliveryAnalyticsRepository"/>. Every query
/// is tenant-scoped at the SQL level (no in-memory cross-tenant
/// filtering) and uses <c>AsNoTracking</c>. There are NO writes,
/// no <c>SaveChanges</c>, and no provider invocations on this
/// path — strictly read-only projection over the existing
/// <c>CustomerStatement</c> delivery columns added by INT-001 /
/// INT-002 / INT-003.
/// </summary>
public sealed class BillingDeliveryAnalyticsRepository : IBillingDeliveryAnalyticsRepository
{
    private readonly BillingDbContext _db;

    public BillingDeliveryAnalyticsRepository(BillingDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<DeliverySummaryRow> GetSummaryAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var baseQuery = _db.CustomerStatements
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId);

        // Total snapshots over the window — bucketed by GeneratedAtUtc
        // so a snapshot that was generated but never sent still
        // surfaces in "visible work this period". The other counts
        // are bucketed by DeliveryAttemptedAtUtc because they are
        // about delivery, not generation.
        var generatedInWindow = baseQuery
            .Where(s => s.GeneratedAtUtc >= fromUtc && s.GeneratedAtUtc <= toUtc);

        var attempted = baseQuery
            .Where(s => s.DeliveryAttemptedAtUtc != null
                        && s.DeliveryAttemptedAtUtc >= fromUtc
                        && s.DeliveryAttemptedAtUtc <= toUtc);

        // Aggregate in a single grouped query against the attempted
        // slice — six tiny conditional sums over a single index range.
        var counts = await attempted
            .GroupBy(_ => 1)
            .Select(g => new
            {
                EverAttempted = g.Count(),
                Sent = g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.Sent),
                Failed = g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.Failed),
                RetryableFailure = g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.RetryableFailure),
                InvalidRecipient = g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.InvalidRecipient),
                ProviderUnavailable = g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.ProviderUnavailable),
            })
            .FirstOrDefaultAsync(ct);

        var totalSnapshots = await generatedInWindow.CountAsync(ct);

        var ever = counts?.EverAttempted ?? 0;
        var sent = counts?.Sent ?? 0;
        var failed = counts?.Failed ?? 0;
        var retryable = counts?.RetryableFailure ?? 0;
        var invalid = counts?.InvalidRecipient ?? 0;
        var unavailable = counts?.ProviderUnavailable ?? 0;
        var classified = sent + failed + retryable + invalid + unavailable;
        var other = ever > classified ? ever - classified : 0;

        return new DeliverySummaryRow(
            WindowStartUtc: fromUtc,
            WindowEndUtc: toUtc,
            TotalSnapshots: totalSnapshots,
            EverAttempted: ever,
            Sent: sent,
            Failed: failed,
            RetryableFailure: retryable,
            InvalidRecipient: invalid,
            ProviderUnavailable: unavailable,
            OtherStatuses: other);
    }

    public async Task<IReadOnlyList<DeliveryTrendBucketRow>> GetTrendAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int maxBuckets,
        CancellationToken ct = default)
    {
        // Daily UTC bucket. We project the raw outcomes in SQL,
        // then bucket in memory — MySQL's DATE() under EF translates
        // unevenly across providers and the row volume here is
        // bounded by `attempted` which is itself bounded by tenant
        // activity in the window.
        var attempted = await _db.CustomerStatements
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId
                        && s.DeliveryAttemptedAtUtc != null
                        && s.DeliveryAttemptedAtUtc >= fromUtc
                        && s.DeliveryAttemptedAtUtc <= toUtc)
            .Select(s => new
            {
                AttemptedAt = s.DeliveryAttemptedAtUtc!.Value,
                Status = s.DeliveryStatus,
            })
            .ToListAsync(ct);

        if (attempted.Count == 0) return Array.Empty<DeliveryTrendBucketRow>();

        var buckets = attempted
            .GroupBy(x => x.AttemptedAt.Date)
            .Select(g => new DeliveryTrendBucketRow(
                BucketDateUtc: g.Key,
                Attempts: g.Count(),
                Sent: g.Count(x => x.Status == StatementDeliveryStatus.Sent),
                Failed: g.Count(x => x.Status == StatementDeliveryStatus.Failed),
                RetryableFailure: g.Count(x => x.Status == StatementDeliveryStatus.RetryableFailure),
                InvalidRecipient: g.Count(x => x.Status == StatementDeliveryStatus.InvalidRecipient),
                ProviderUnavailable: g.Count(x => x.Status == StatementDeliveryStatus.ProviderUnavailable)))
            .OrderByDescending(b => b.BucketDateUtc)
            .Take(maxBuckets > 0 ? maxBuckets : 30)
            .ToList();

        return buckets;
    }

    public async Task<RetryAnalyticsRow> GetRetryAnalyticsAsync(
        Guid tenantId,
        int maxAttempts,
        int cooldownSeconds,
        DateTime nowUtc,
        int topN,
        CancellationToken ct = default)
    {
        var safeMax = maxAttempts > 0 ? maxAttempts : 5;
        var safeCooldown = cooldownSeconds >= 0 ? cooldownSeconds : 60;
        var safeTopN = topN switch { <= 0 => 10, > 50 => 50, _ => topN };
        var cooldownCutoff = nowUtc.AddSeconds(-safeCooldown);

        var baseQuery = _db.CustomerStatements
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId);

        // Three small scalar counts in parallel against the same
        // index range. The cooldown predicate uses the SAME math
        // the orchestrator uses (DeliveryAttemptedAtUtc +
        // CooldownSeconds), so the count reconciles 1:1 with the
        // governance evaluator at request time.
        var atLimitTask = baseQuery
            .CountAsync(s => s.DeliveryRetryCount >= safeMax, ct);
        var inCooldownTask = baseQuery
            .CountAsync(s => s.DeliveryAttemptedAtUtc != null
                             && s.DeliveryAttemptedAtUtc > cooldownCutoff
                             && s.DeliveryStatus != StatementDeliveryStatus.Sent
                             && s.DeliveryRetryCount < safeMax, ct);
        var anyRetryTask = baseQuery.CountAsync(s => s.DeliveryRetryCount > 0, ct);
        var totalRetriesTask = baseQuery.SumAsync(s => (int?)s.DeliveryRetryCount, ct);

        await Task.WhenAll(atLimitTask, inCooldownTask, anyRetryTask, totalRetriesTask);

        var top = await baseQuery
            .Where(s => s.DeliveryRetryCount > 0)
            .OrderByDescending(s => s.DeliveryRetryCount)
            .ThenByDescending(s => s.DeliveryAttemptedAtUtc)
            .Take(safeTopN)
            .Select(s => new TopRetriedSnapshotRow(
                s.Id,
                s.StatementNumber,
                s.CustomerId,
                s.DeliveryRetryCount,
                s.DeliveryStatus,
                s.DeliveryFailureReason,
                s.DeliveryAttemptedAtUtc))
            .ToListAsync(ct);

        return new RetryAnalyticsRow(
            MaxAttemptsConfigured: safeMax,
            CooldownSecondsConfigured: safeCooldown,
            SnapshotsAtRetryLimit: atLimitTask.Result,
            SnapshotsInCooldownNow: inCooldownTask.Result,
            SnapshotsWithAnyRetry: anyRetryTask.Result,
            TotalRetryAttemptsRecorded: totalRetriesTask.Result ?? 0,
            TopRetried: top);
    }

    public async Task<IReadOnlyList<ProviderLifetimeRow>> GetProviderLifetimeAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        // One row per (DeliveryProvider) ever recorded. NULL
        // providers (snapshots never attempted) are excluded by
        // the WHERE — the analytics surface is "what did each
        // provider do for this tenant", not "how many snapshots
        // exist".
        var rows = await _db.CustomerStatements
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.DeliveryProvider != null)
            .GroupBy(s => s.DeliveryProvider!)
            .Select(g => new ProviderLifetimeRow(
                g.Key,
                g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.Sent),
                g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.Failed
                             || s.DeliveryStatus == StatementDeliveryStatus.ProviderUnavailable
                             || s.DeliveryStatus == StatementDeliveryStatus.RetryableFailure),
                g.Count(s => s.DeliveryStatus == StatementDeliveryStatus.RetryableFailure),
                g.Max(s => s.DeliveryStatus == StatementDeliveryStatus.Sent ? s.DeliveryLastSentAtUtc : null),
                g.Max(s => s.DeliveryStatus != StatementDeliveryStatus.Sent && s.DeliveryStatus != null
                           ? s.DeliveryAttemptedAtUtc
                           : null)))
            .ToListAsync(ct);

        return rows;
    }
}
