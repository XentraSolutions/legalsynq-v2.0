using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

internal sealed class EfSyncStateService : ISyncStateService
{
    private readonly XeniaDbContext _db;
    private readonly IProviderCursorProtector _cursorProtector;
    private readonly ILogger<EfSyncStateService> _logger;

    public EfSyncStateService(
        XeniaDbContext db,
        IProviderCursorProtector cursorProtector,
        ILogger<EfSyncStateService> logger)
    {
        _db              = db;
        _cursorProtector = cursorProtector;
        _logger          = logger;
    }

    public async Task<EmailSyncState> GetOrCreateAsync(
        Guid tenantId, Guid emailSourceId, EmailProviderType providerType, CancellationToken ct = default)
    {
        var existing = await _db.EmailSyncStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EmailSourceId == emailSourceId, ct);

        if (existing is not null) return existing;

        var state = EmailSyncState.Create(tenantId, emailSourceId, providerType);
        _db.EmailSyncStates.Add(state);
        await _db.SaveChangesAsync(ct);
        return state;
    }

    public async Task<EmailIngestionRun> StartRunAsync(
        Guid tenantId, Guid emailSourceId, IngestionRunTriggerType triggerType,
        string? correlationId, Guid? actorId, string? workerInstanceId,
        string? cursorBeforeSafeSummary, CancellationToken ct = default)
    {
        var run = EmailIngestionRun.Create(tenantId, emailSourceId, triggerType,
            correlationId, actorId, workerInstanceId, cursorBeforeSafeSummary);
        _db.EmailIngestionRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task MarkRunStartedAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.EmailIngestionRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        run.MarkRunning();
        await _db.SaveChangesAsync(ct);
    }

    public async Task CommitCursorAsync(
        Guid tenantId, Guid emailSourceId, ProviderSyncCursor cursor,
        DateTime? lastProcessedTimestamp, string? lastProcessedMessageId, CancellationToken ct = default)
    {
        var state = await _db.EmailSyncStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EmailSourceId == emailSourceId, ct);
        if (state is null) return;

        // Protect cursor value before persisting — never store raw tokens in DB
        string? protectedCursorValue = null;
        if (cursor.RawValue is not null)
        {
            try
            {
                protectedCursorValue = await _cursorProtector.ProtectAsync(
                    cursor.RawValue, tenantId, emailSourceId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Cursor protection failed for tenantId={TenantId} sourceId={SourceId} — cursor not committed",
                    tenantId, emailSourceId);
                return;
            }
        }

        state.CommitCursor(
            protectedCursorValue, cursor.MetadataJson, cursor.SafeSummary,
            lastProcessedTimestamp, lastProcessedMessageId);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Cursor commit concurrency conflict for tenantId={TenantId} sourceId={SourceId}",
                tenantId, emailSourceId);
        }
    }

    public async Task RecordFailureAsync(
        Guid tenantId, Guid emailSourceId, string errorCode, string safeErrorSummary, CancellationToken ct = default)
    {
        var state = await _db.EmailSyncStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EmailSourceId == emailSourceId, ct);
        if (state is null) return;

        var backoff = ComputeBackoff(state.ConsecutiveFailureCount);
        state.RecordFailure(errorCode, safeErrorSummary, DateTime.UtcNow.Add(backoff));
        await _db.SaveChangesAsync(ct);
    }

    public async Task CompleteRunAsync(Guid runId, string? cursorAfterSafeSummary, CancellationToken ct = default)
    {
        var run = await _db.EmailIngestionRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        run.Complete(cursorAfterSafeSummary);
        await _db.SaveChangesAsync(ct);
    }

    public async Task FailRunAsync(Guid runId, string errorCode, string safeErrorSummary, CancellationToken ct = default)
    {
        var run = await _db.EmailIngestionRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        run.Fail(errorCode, safeErrorSummary);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResetCursorAsync(Guid tenantId, Guid emailSourceId, string reason, CancellationToken ct = default)
    {
        var state = await _db.EmailSyncStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EmailSourceId == emailSourceId, ct);
        if (state is null) return;
        state.ResetCursor(reason);
        await _db.SaveChangesAsync(ct);
    }

    public Task<EmailSyncState?> GetSyncStateAsync(Guid tenantId, Guid emailSourceId, CancellationToken ct = default)
        => _db.EmailSyncStates
              .AsNoTracking()
              .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EmailSourceId == emailSourceId, ct);

    public async Task<IReadOnlyList<EmailIngestionRun>> GetIngestionHistoryAsync(
        Guid tenantId, Guid emailSourceId, int pageSize, int pageOffset, CancellationToken ct = default)
    {
        return await _db.EmailIngestionRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.EmailSourceId == emailSourceId)
            .OrderByDescending(r => r.StartedAt)
            .Skip(pageOffset)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<EmailIngestionRun?> GetRunAsync(Guid tenantId, Guid runId, CancellationToken ct = default)
        => _db.EmailIngestionRuns
              .AsNoTracking()
              .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId, ct);

    public Task<EmailIngestionRun?> GetActiveRunAsync(Guid tenantId, Guid emailSourceId, CancellationToken ct = default)
        => _db.EmailIngestionRuns
              .AsNoTracking()
              .FirstOrDefaultAsync(r =>
                  r.TenantId == tenantId && r.EmailSourceId == emailSourceId
                  && (r.Status == IngestionRunStatus.Queued || r.Status == IngestionRunStatus.Running), ct);

    public async Task UpdateRunCountersAsync(Guid runId, EmailIngestionRun counters, CancellationToken ct = default)
    {
        var run = await _db.EmailIngestionRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        await _db.SaveChangesAsync(ct);
    }

    private static TimeSpan ComputeBackoff(int failureCount)
    {
        var baseSeconds = Math.Pow(2, Math.Min(failureCount, 8)) * 5;
        return TimeSpan.FromSeconds(Math.Min(baseSeconds, 3600));
    }
}
