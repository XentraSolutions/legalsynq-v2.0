using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.TenantBilling.Outbox;

/// <summary>
/// TB-INT-04 — EF-backed
/// <see cref="ITenantBillingEntitlementOutbox"/>. Writes a single
/// <see cref="TenantBillingEntitlementPublishOutboxRow"/> per call,
/// catches and logs any persistence exception so the caller's just-
/// committed Commerce transaction is never rolled back by an outbox
/// failure.
/// </summary>
internal sealed class EfTenantBillingEntitlementOutbox : ITenantBillingEntitlementOutbox
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly TenantBillingClientOptions _opts;
    private readonly TenantBillingPublisherMetrics _metrics;
    private readonly ILogger<EfTenantBillingEntitlementOutbox> _log;

    public EfTenantBillingEntitlementOutbox(
        CommerceDbContext db,
        IClock clock,
        IOptions<TenantBillingClientOptions> options,
        TenantBillingPublisherMetrics metrics,
        ILogger<EfTenantBillingEntitlementOutbox> log)
    {
        _db = db;
        _clock = clock;
        _opts = options.Value.Normalised();
        _metrics = metrics;
        _log = log;
    }

    public async Task<Guid> EnqueueAsync(
        Guid billingAccountId,
        string triggerSource,
        string? correlationId,
        CancellationToken ct)
    {
        if (billingAccountId == Guid.Empty || string.IsNullOrWhiteSpace(triggerSource))
        {
            _metrics.RecordOutboxEnqueueFailed(triggerSource ?? "unknown", "invalid");
            _log.LogWarning(
                "Outbox enqueue rejected (invalid input): BA={BillingAccountId} Trigger={TriggerSource}",
                billingAccountId, triggerSource);
            return Guid.Empty;
        }

        try
        {
            var row = TenantBillingEntitlementPublishOutboxRow.Create(
                billingAccountId,
                triggerSource,
                correlationId,
                _opts.OutboxMaxAttempts,
                _clock.UtcNow);
            _db.Set<TenantBillingEntitlementPublishOutboxRow>().Add(row);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            _metrics.RecordOutboxEnqueued(triggerSource);
            _log.LogInformation(
                "Outbox enqueue accepted: OutboxId={OutboxId} BA={BillingAccountId} Trigger={TriggerSource} MaxAttempts={MaxAttempts}",
                row.Id, billingAccountId, triggerSource, row.MaxAttempts);
            return row.Id;
        }
        catch (Exception ex)
        {
            _metrics.RecordOutboxEnqueueFailed(triggerSource, "exception");
            _log.LogError(ex,
                "Outbox enqueue failed: BA={BillingAccountId} Trigger={TriggerSource}",
                billingAccountId, triggerSource);
            return Guid.Empty;
        }
    }

    public async Task<TenantBillingEntitlementOutboxCounts> GetCountsAsync(CancellationToken ct)
    {
        var set = _db.Set<TenantBillingEntitlementPublishOutboxRow>().AsNoTracking();
        var grouped = await set
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int Of(TenantBillingEntitlementPublishOutboxStatus s) =>
            grouped.FirstOrDefault(x => x.Key == s)?.Count ?? 0;

        return new TenantBillingEntitlementOutboxCounts(
            Pending:    Of(TenantBillingEntitlementPublishOutboxStatus.Pending),
            Processing: Of(TenantBillingEntitlementPublishOutboxStatus.Processing),
            Published:  Of(TenantBillingEntitlementPublishOutboxStatus.Published),
            Failed:     Of(TenantBillingEntitlementPublishOutboxStatus.Failed),
            Abandoned:  Of(TenantBillingEntitlementPublishOutboxStatus.Abandoned));
    }
}
