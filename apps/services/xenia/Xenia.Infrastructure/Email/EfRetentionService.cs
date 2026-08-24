using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Tenant-scoped retention execution service.
///
/// Runs in dry-run or execute mode depending on mode parameter.
/// Dry-run counts eligible records without deleting.
/// Execute mode batch-deletes in chunks of PurgeBatchSize.
///
/// Safety rules enforced:
/// - Legal hold blocks all deletion
/// - Active sources' sync state is never deleted
/// - Open alerts are never deleted
/// - Active runs are not deleted
/// </summary>
internal sealed class EfRetentionService : IRetentionService
{
    private readonly XeniaDbContext _db;
    private readonly IEmailOperationalSettingsService _settingsService;
    private readonly IAuditAdapter _auditAdapter;
    private readonly ILogger<EfRetentionService> _logger;

    public EfRetentionService(
        XeniaDbContext db,
        IEmailOperationalSettingsService settingsService,
        IAuditAdapter auditAdapter,
        ILogger<EfRetentionService> logger)
    {
        _db              = db;
        _settingsService = settingsService;
        _auditAdapter    = auditAdapter;
        _logger          = logger;
    }

    public async Task<EmailRetentionRun> ExecuteAsync(
        Guid tenantId,
        EmailRetentionMode mode,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default)
    {
        var settings = await _settingsService.GetOrCreateAsync(tenantId, ct);

        if (!settings.RetentionEnabled && mode == EmailRetentionMode.Execute)
        {
            var disabled = EmailRetentionRun.Create(tenantId, mode, actorId, correlationId);
            disabled.Fail("Retention is disabled for this tenant.");
            _db.EmailRetentionRuns.Add(disabled);
            await _db.SaveChangesAsync(ct);
            return disabled;
        }

        if (settings.LegalHoldEnabled)
        {
            var held = EmailRetentionRun.Create(tenantId, mode, actorId, correlationId);
            held.Fail("Legal hold is enabled — no data may be deleted.");
            _db.EmailRetentionRuns.Add(held);
            await _db.SaveChangesAsync(ct);
            return held;
        }

        var run = EmailRetentionRun.Create(tenantId, mode, actorId, correlationId);
        _db.EmailRetentionRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        try
        {
            var now              = DateTime.UtcNow;
            var batchSize        = settings.PurgeBatchSize;
            var isDryRun         = mode == EmailRetentionMode.DryRun;

            var msgCutoff        = now.AddDays(-settings.MessageMetadataRetentionDays);
            var runCutoff        = now.AddDays(-settings.IngestionRunRetentionDays);
            var alertCutoff      = now.AddDays(-settings.AlertRetentionDays);
            var attachCutoff     = now.AddDays(-settings.AttachmentReferenceRetentionDays);

            // ── Eligible counts ───────────────────────────────────────────────
            var messagesEligible = await _db.EmailMessages
                .CountAsync(m => m.TenantId == tenantId && m.ReceivedAt < msgCutoff, ct);

            var runsEligible = await _db.EmailIngestionRuns
                .CountAsync(r => r.TenantId == tenantId &&
                                 r.StartedAt < runCutoff &&
                                 r.IsTerminal, ct);

            var alertsEligible = await _db.EmailOperationalAlerts
                .CountAsync(a => a.TenantId == tenantId &&
                                 a.CreatedAt < alertCutoff &&
                                 a.Status == EmailAlertStatus.Resolved, ct);

            var attachEligible = await _db.EmailAttachmentReferences
                .CountAsync(a => a.TenantId == tenantId && a.CreatedAtUtc < attachCutoff, ct);

            int messagesDeleted = 0, runsDeleted = 0, alertsDeleted = 0, attachDeleted = 0;
            int failures        = 0;

            if (!isDryRun)
            {
                // Delete in batches
                messagesDeleted = await DeleteInBatchesAsync(
                    () => _db.EmailMessages
                            .Where(m => m.TenantId == tenantId && m.ReceivedAt < msgCutoff)
                            .OrderBy(m => m.Id)
                            .Take(batchSize),
                    batchSize, ct);

                runsDeleted = await DeleteInBatchesAsync(
                    () => _db.EmailIngestionRuns
                            .Where(r => r.TenantId == tenantId && r.StartedAt < runCutoff && r.IsTerminal)
                            .OrderBy(r => r.Id)
                            .Take(batchSize),
                    batchSize, ct);

                alertsDeleted = await DeleteInBatchesAsync(
                    () => _db.EmailOperationalAlerts
                            .Where(a => a.TenantId == tenantId &&
                                        a.CreatedAt < alertCutoff &&
                                        a.Status == EmailAlertStatus.Resolved)
                            .OrderBy(a => a.Id)
                            .Take(batchSize),
                    batchSize, ct);

                attachDeleted = await DeleteInBatchesAsync(
                    () => _db.EmailAttachmentReferences
                            .Where(a => a.TenantId == tenantId && a.CreatedAtUtc < attachCutoff)
                            .OrderBy(a => a.Id)
                            .Take(batchSize),
                    batchSize, ct);
            }

            run.RecordProgress(
                messagesEligible:            messagesEligible,
                messagesDeleted:             messagesDeleted,
                bodiesCleared:               0,
                runsDeleted:                 runsDeleted,
                alertsDeleted:               alertsDeleted,
                attachmentReferencesDeleted: attachDeleted,
                failures:                    failures);

            run.Complete();
            await _db.SaveChangesAsync(ct);

            await TryAuditAsync(new XeniaAuditEvent
            {
                Action        = "xenia.email.retention.completed",
                ResourceType  = "email_retention_run",
                ResourceId    = run.Id.ToString(),
                Result        = "success",
                TenantId      = tenantId,
                ActorId       = actorId,
                CorrelationId = correlationId,
                OccurredAt    = DateTime.UtcNow,
                Detail        = $"mode={mode} messagesDeleted={messagesDeleted} runsDeleted={runsDeleted}",
            });

            return run;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retention run failed: tenant={TenantId} runId={RunId}", tenantId, run.Id);
            run.Fail("Retention run encountered an unexpected error.");
            await _db.SaveChangesAsync(ct);

            await TryAuditAsync(new XeniaAuditEvent
            {
                Action        = "xenia.email.retention.failed",
                ResourceType  = "email_retention_run",
                ResourceId    = run.Id.ToString(),
                Result        = "failure",
                TenantId      = tenantId,
                ActorId       = actorId,
                CorrelationId = correlationId,
                OccurredAt    = DateTime.UtcNow,
            });

            return run;
        }
    }

    public async Task<IReadOnlyList<EmailRetentionRun>> GetHistoryAsync(
        Guid tenantId, int limit = 20, CancellationToken ct = default)
        => await _db.EmailRetentionRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(ct);

    private async Task<int> DeleteInBatchesAsync<T>(
        Func<IQueryable<T>> query, int batchSize, CancellationToken ct) where T : class
    {
        int total = 0;
        while (!ct.IsCancellationRequested)
        {
            var batch = await query().ToListAsync(ct);
            if (batch.Count == 0) break;
            _db.Set<T>().RemoveRange(batch);
            await _db.SaveChangesAsync(ct);
            total += batch.Count;
            if (batch.Count < batchSize) break;
        }
        return total;
    }

    private async Task TryAuditAsync(XeniaAuditEvent evt)
    {
        try { await _auditAdapter.RecordEventAsync(evt); }
        catch (Exception ex) { _logger.LogWarning(ex, "Audit emit failed for {Action}", evt.Action); }
    }
}
