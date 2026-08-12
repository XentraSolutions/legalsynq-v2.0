using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// EF-backed run query, retry, and cancellation service.
/// Tenant-scoped — cross-tenant access returns null/404.
/// </summary>
internal sealed class EfRunQueryService : IRunQueryService
{
    private readonly XeniaDbContext _db;
    private readonly IAuditAdapter _auditAdapter;
    private readonly ILogger<EfRunQueryService> _logger;

    public EfRunQueryService(
        XeniaDbContext db,
        IAuditAdapter auditAdapter,
        ILogger<EfRunQueryService> logger)
    {
        _db           = db;
        _auditAdapter = auditAdapter;
        _logger       = logger;
    }

    public async Task<RunPageResult> ListAsync(RunListQuery query, CancellationToken ct = default)
    {
        var q = _db.EmailIngestionRuns
            .AsNoTracking()
            .Where(r => r.TenantId == query.TenantId);

        if (query.SourceId.HasValue)    q = q.Where(r => r.EmailSourceId == query.SourceId.Value);
        if (query.Status.HasValue)      q = q.Where(r => r.Status == query.Status.Value);
        if (query.Trigger.HasValue)     q = q.Where(r => r.TriggerType == query.Trigger.Value);
        if (query.HasErrors.HasValue && query.HasErrors.Value)
            q = q.Where(r => r.MessagesFailed > 0 || r.AttachmentsFailed > 0);
        if (query.CorrelationId is not null)
            q = q.Where(r => r.CorrelationId == query.CorrelationId);
        if (query.WorkerInstanceId is not null)
            q = q.Where(r => r.WorkerInstanceId == query.WorkerInstanceId);
        if (query.From.HasValue) q = q.Where(r => r.StartedAt >= query.From.Value.UtcDateTime);
        if (query.To.HasValue)   q = q.Where(r => r.StartedAt <= query.To.Value.UtcDateTime);

        if (query.Provider.HasValue)
        {
            var sourceIds = await _db.EmailSources
                .AsNoTracking()
                .Where(s => s.TenantId == query.TenantId && s.ProviderType == query.Provider.Value)
                .Select(s => s.Id)
                .ToListAsync(ct);
            q = q.Where(r => sourceIds.Contains(r.EmailSourceId));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(r => r.StartedAt)
            .ThenBy(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new RunPageResult(items, total, query.Page, query.PageSize);
    }

    public async Task<RunDetailResult?> GetDetailAsync(Guid tenantId, Guid runId, CancellationToken ct = default)
    {
        var run = await _db.EmailIngestionRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId, ct);

        if (run is null) return null;

        return new RunDetailResult(
            Run:                    run,
            SafeCursorBeforeSummary: run.CursorBeforeSafeSummary,
            SafeCursorAfterSummary:  run.CursorAfterSafeSummary);
    }

    public async Task<RunRetryResult> RetryAsync(
        Guid tenantId, Guid runId, Guid? actorId, string? correlationId, CancellationToken ct = default)
    {
        var run = await _db.EmailIngestionRuns
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId, ct);

        if (run is null)
            return new RunRetryResult(false, null, "not_found", "Run not found.");

        if (run.Status != IngestionRunStatus.Failed &&
            run.Status != IngestionRunStatus.CompletedWithErrors)
            return new RunRetryResult(false, null, "not_retryable",
                $"Only Failed or CompletedWithErrors runs are retryable. Current status: {run.Status}");

        // Check source still enabled
        var source = await _db.EmailSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == run.EmailSourceId, ct);

        if (source is null || source.Status == EmailSourceStatus.Disabled)
            return new RunRetryResult(false, null, "source_disabled", "Source is disabled or not found.");

        // Check lock
        var lockRow = await _db.EmailSourceSyncLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.EmailSourceId == run.EmailSourceId, ct);

        if (lockRow is not null && !lockRow.IsExpired)
            return new RunRetryResult(false, null, "source_locked", "Source is currently locked.");

        // Create retry run
        var retryRun = EmailIngestionRun.CreateRetry(
            tenantId, run.EmailSourceId, run.Id, actorId, correlationId);

        _db.EmailIngestionRuns.Add(retryRun);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Retry queued: originalRunId={OriginalId} newRunId={NewId} tenant={TenantId}",
            runId, retryRun.Id, tenantId);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action       = "xenia.email.sync.retry_queued",
            ResourceType = "email_ingestion_run",
            ResourceId   = retryRun.Id.ToString(),
            Result       = "success",
            TenantId     = tenantId,
            ActorId      = actorId,
            CorrelationId= correlationId,
            OccurredAt   = DateTime.UtcNow,
            Detail       = $"originalRunId={runId}",
        });

        return new RunRetryResult(true, retryRun.Id, null, null);
    }

    public async Task<RunCancellationResult> CancelAsync(
        Guid tenantId, Guid runId, Guid? actorId, string? correlationId, CancellationToken ct = default)
    {
        var run = await _db.EmailIngestionRuns
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId, ct);

        if (run is null)
            return new RunCancellationResult("NotFound", "Run not found.");

        if (run.Status == IngestionRunStatus.Completed ||
            run.Status == IngestionRunStatus.CompletedWithErrors ||
            run.Status == IngestionRunStatus.Cancelled)
            return new RunCancellationResult("AlreadyCompleted",
                $"Run already in terminal state: {run.Status}");

        run.Cancel();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Run cancelled: runId={RunId} tenant={TenantId}", runId, tenantId);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action       = "xenia.email.sync.cancelled",
            ResourceType = "email_ingestion_run",
            ResourceId   = runId.ToString(),
            Result       = "success",
            TenantId     = tenantId,
            ActorId      = actorId,
            CorrelationId= correlationId,
            OccurredAt   = DateTime.UtcNow,
        });

        return new RunCancellationResult("CancellationRequested", null);
    }

    private async Task TryAuditAsync(XeniaAuditEvent evt)
    {
        try { await _auditAdapter.RecordEventAsync(evt); }
        catch (Exception ex) { _logger.LogWarning(ex, "Audit emit failed for {Action}", evt.Action); }
    }
}
