using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.TenantBilling.Outbox;

/// <summary>
/// TB-INT-04 — default
/// <see cref="ITenantBillingEntitlementOutboxProcessor"/>. Per
/// invocation it (1) recovers stale <c>Processing</c> rows, then
/// (2) claims and processes up to <c>batchSize</c> due rows.
///
/// <para>Uses optimistic claim semantics on EF: read candidate ids,
/// then update each row from <c>Pending</c> to <c>Processing</c>
/// guarded by a status filter. Two workers racing on the same row
/// will both attempt the update; only one will see a row affected
/// (the other skips). On the InMemory provider used in tests there
/// is only one worker, so the optimistic guard is sufficient.</para>
///
/// <para>Per-row publisher exceptions are caught and recorded as a
/// retry/abandon; one bad row never stops the batch.</para>
/// </summary>
internal sealed class TenantBillingEntitlementOutboxProcessor
    : ITenantBillingEntitlementOutboxProcessor
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly ITenantBillingEntitlementPublisher _publisher;
    private readonly TenantBillingClientOptions _opts;
    private readonly TenantBillingPublisherMetrics _metrics;
    private readonly ILogger<TenantBillingEntitlementOutboxProcessor> _log;

    public TenantBillingEntitlementOutboxProcessor(
        CommerceDbContext db,
        IClock clock,
        ITenantBillingEntitlementPublisher publisher,
        IOptions<TenantBillingClientOptions> options,
        TenantBillingPublisherMetrics metrics,
        ILogger<TenantBillingEntitlementOutboxProcessor> log)
    {
        _db = db;
        _clock = clock;
        _publisher = publisher;
        _opts = options.Value.Normalised();
        _metrics = metrics;
        _log = log;
    }

    /// <summary>
    /// Reasons that mean "this row will never succeed even after
    /// infinite retries" — caused by missing/invalid Commerce-side
    /// data, not transient infra failures. Mapped to
    /// <c>Abandoned</c> on the first publisher response.
    /// </summary>
    private static readonly HashSet<string> AbandonOnSkipReasons = new(StringComparer.Ordinal)
    {
        "no-external-tenant-id",
        "external-tenant-id-not-a-guid",
        "billing-account-not-found",
        "tenant-id-empty",
    };

    public async Task<TenantBillingEntitlementOutboxBatchResult> ProcessDueAsync(
        int batchSize,
        CancellationToken ct)
    {
        if (batchSize < 1) batchSize = 1;
        var nowUtc = _clock.UtcNow;
        var staleThresholdSeconds = Math.Max(_opts.OutboxPollSeconds * 3, 60);
        var staleBefore = nowUtc.AddSeconds(-staleThresholdSeconds);

        // (1) Recover stale Processing rows. Don't count toward batch
        // budget — they're cheap and the next poll picks them up as
        // ordinary Pending rows.
        var recovered = await RecoverStaleProcessingAsync(staleBefore, nowUtc, ct).ConfigureAwait(false);

        // (2) Find due Pending rows.
        var set = _db.Set<TenantBillingEntitlementPublishOutboxRow>();
        var dueIds = await set
            .Where(r => r.Status == TenantBillingEntitlementPublishOutboxStatus.Pending
                        && r.NextAttemptAtUtc <= nowUtc
                        && r.Attempts < r.MaxAttempts)
            .OrderBy(r => r.NextAttemptAtUtc)
            .ThenBy(r => r.CreatedAtUtc)
            .Select(r => r.Id)
            .Take(batchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int published = 0, retried = 0, abandoned = 0, skipped = 0;

        foreach (var id in dueIds)
        {
            ct.ThrowIfCancellationRequested();
            var processed = await ProcessOneAsync(id, ct).ConfigureAwait(false);
            switch (processed)
            {
                case OneOutcome.Published: published++; break;
                case OneOutcome.Retried:   retried++;   break;
                case OneOutcome.Abandoned: abandoned++; break;
                case OneOutcome.Skipped:   skipped++;   break;
            }
        }

        if (dueIds.Count > 0 || recovered > 0)
        {
            _log.LogInformation(
                "Outbox batch completed: Considered={Considered} Recovered={Recovered} Published={Published} Retried={Retried} Abandoned={Abandoned} Skipped={Skipped}",
                dueIds.Count, recovered, published, retried, abandoned, skipped);
        }

        return new TenantBillingEntitlementOutboxBatchResult(
            Considered: dueIds.Count,
            Recovered: recovered,
            Published: published,
            Retried: retried,
            Abandoned: abandoned,
            Skipped: skipped);
    }

    private async Task<int> RecoverStaleProcessingAsync(
        DateTime staleBefore, DateTime nowUtc, CancellationToken ct)
    {
        var set = _db.Set<TenantBillingEntitlementPublishOutboxRow>();
        var stale = await set
            .Where(r => r.Status == TenantBillingEntitlementPublishOutboxStatus.Processing
                        && r.LockedAtUtc != null
                        && r.LockedAtUtc < staleBefore)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (stale.Count == 0) return 0;

        foreach (var row in stale)
        {
            row.RecoverStaleProcessing(nowUtc);
            _log.LogWarning(
                "Outbox stale Processing row recovered: OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} LockId={LockId}",
                row.Id, row.BillingAccountId, row.TriggerSource, row.LockId);
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return stale.Count;
    }

    private enum OneOutcome { Skipped, Published, Retried, Abandoned, Lost }

    private async Task<OneOutcome> ProcessOneAsync(Guid id, CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;
        var row = await _db.Set<TenantBillingEntitlementPublishOutboxRow>()
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);
        if (row is null
            || row.Status != TenantBillingEntitlementPublishOutboxStatus.Pending
            || row.NextAttemptAtUtc > nowUtc
            || row.Attempts >= row.MaxAttempts)
        {
            return OneOutcome.Lost;
        }

        var lockId = Guid.CreateVersion7();
        row.MarkProcessing(lockId, nowUtc);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            _log.LogDebug(
                "Outbox claim lost (concurrency): OutboxId={OutboxId}", id);
            return OneOutcome.Lost;
        }

        _log.LogDebug(
            "Outbox item processing started: OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} Attempts={Attempts} MaxAttempts={MaxAttempts} LockId={LockId}",
            row.Id, row.BillingAccountId, row.TriggerSource, row.Attempts, row.MaxAttempts, lockId);

        PublishEntitlementResult result;
        try
        {
            result = await _publisher
                .PublishForBillingAccountAsync(row.BillingAccountId, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Don't mutate state; the row is left in Processing and the
            // stale-recovery sweep on the next poll will return it to
            // Pending.
            throw;
        }
        catch (Exception ex)
        {
            return await HandleExceptionAsync(row, ex, ct).ConfigureAwait(false);
        }

        return await HandleResultAsync(row, result, ct).ConfigureAwait(false);
    }

    private async Task<OneOutcome> HandleResultAsync(
        TenantBillingEntitlementPublishOutboxRow row,
        PublishEntitlementResult result,
        CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;
        _metrics.RecordOutboxProcessed(row.TriggerSource, result.Outcome.ToString().ToLowerInvariant(), result.Reason);
        switch (result.Outcome)
        {
            case PublishEntitlementOutcome.Published:
                row.MarkPublished(result.Reason, result.HttpStatus, nowUtc);
                _metrics.RecordOutboxPublished(row.TriggerSource);
                _log.LogInformation(
                    "Outbox item published: OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} HttpStatus={HttpStatus} Attempts={Attempts}",
                    row.Id, row.BillingAccountId, row.TriggerSource, result.HttpStatus, row.Attempts);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                return OneOutcome.Published;

            case PublishEntitlementOutcome.Skipped:
                if (AbandonOnSkipReasons.Contains(result.Reason))
                {
                    row.MarkAbandoned("skipped", result.Reason, result.HttpStatus, errorSummary: null, nowUtc);
                    _metrics.RecordOutboxAbandoned(row.TriggerSource, result.Reason);
                    _log.LogWarning(
                        "Outbox item abandoned (terminal skip reason): OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} Reason={Reason}",
                        row.Id, row.BillingAccountId, row.TriggerSource, result.Reason);
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return OneOutcome.Abandoned;
                }
                else
                {
                    var next = nowUtc.AddSeconds(_opts.OutboxRetryBaseDelaySeconds);
                    row.RescheduleSkipped(result.Reason, next, nowUtc);
                    _log.LogInformation(
                        "Outbox item skipped (transient gating); rescheduled: OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} Reason={Reason} NextAttemptAtUtc={NextAttemptAtUtc}",
                        row.Id, row.BillingAccountId, row.TriggerSource, result.Reason, next);
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return OneOutcome.Skipped;
                }

            case PublishEntitlementOutcome.Failed:
            default:
                return await ScheduleRetryOrAbandonAsync(row, result.Reason, result.HttpStatus, result.ResponseBodySummary, nowUtc, ct).ConfigureAwait(false);
        }
    }

    private async Task<OneOutcome> HandleExceptionAsync(
        TenantBillingEntitlementPublishOutboxRow row,
        Exception ex,
        CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;
        _metrics.RecordOutboxProcessed(row.TriggerSource, "failed", "exception");
        _log.LogError(ex,
            "Outbox publisher threw: OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource}",
            row.Id, row.BillingAccountId, row.TriggerSource);
        return await ScheduleRetryOrAbandonAsync(row, "exception", httpStatus: null,
            errorSummary: Truncate(ex.Message, 2000), nowUtc, ct).ConfigureAwait(false);
    }

    private async Task<OneOutcome> ScheduleRetryOrAbandonAsync(
        TenantBillingEntitlementPublishOutboxRow row,
        string reason,
        int? httpStatus,
        string? errorSummary,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var nextAttempts = row.Attempts + 1;
        if (nextAttempts >= row.MaxAttempts)
        {
            row.MarkAbandoned("failed", reason, httpStatus, errorSummary, nowUtc);
            _metrics.RecordOutboxAbandoned(row.TriggerSource, reason);
            _log.LogWarning(
                "Outbox item abandoned (max attempts reached): OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} Attempts={Attempts} MaxAttempts={MaxAttempts} Reason={Reason}",
                row.Id, row.BillingAccountId, row.TriggerSource, nextAttempts, row.MaxAttempts, reason);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return OneOutcome.Abandoned;
        }

        var multiplier = Math.Min(nextAttempts, 10);
        var next = nowUtc.AddSeconds(_opts.OutboxRetryBaseDelaySeconds * multiplier);
        row.MarkFailedAndScheduleRetry(reason, httpStatus, errorSummary, next, nowUtc);
        _metrics.RecordOutboxFailed(row.TriggerSource, reason);
        _metrics.RecordOutboxRetried(row.TriggerSource);
        _log.LogWarning(
            "Outbox item failed; retry scheduled: OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} Attempts={Attempts} HttpStatus={HttpStatus} Reason={Reason} NextAttemptAtUtc={NextAttemptAtUtc}",
            row.Id, row.BillingAccountId, row.TriggerSource, nextAttempts, httpStatus, reason, next);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return OneOutcome.Retried;
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s!.Length > max ? s[..max] : s);
}
