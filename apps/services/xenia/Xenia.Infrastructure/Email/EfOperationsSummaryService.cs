using Microsoft.EntityFrameworkCore;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Database-aggregation backed operations summary service.
/// Uses LINQ grouping and counting — never loads full record sets into memory.
/// </summary>
internal sealed class EfOperationsSummaryService : IOperationsSummaryService
{
    private readonly XeniaDbContext _db;

    public EfOperationsSummaryService(XeniaDbContext db) => _db = db;

    public async Task<OperationsSummaryResult> GetSummaryAsync(
        OperationsSummaryQuery query, CancellationToken ct = default)
    {
        var tenantId = query.TenantId;
        var fromUtc  = query.From?.UtcDateTime;
        var toUtc    = query.To?.UtcDateTime;

        // ── Sources ───────────────────────────────────────────────────────────
        var sourceQ = _db.EmailSources
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted);

        if (query.Provider.HasValue)
            sourceQ = sourceQ.Where(s => s.ProviderType == query.Provider.Value);

        if (query.SourceId.HasValue)
            sourceQ = sourceQ.Where(s => s.Id == query.SourceId.Value);

        var sourceCounts = await sourceQ
            .GroupBy(s => 1)
            .Select(g => new
            {
                Total         = g.Count(),
                Enabled       = g.Count(s => s.Status != EmailSourceStatus.Disabled),
                Healthy       = g.Count(s => s.HealthStatus == EmailHealthStatus.Healthy),
                Degraded      = g.Count(s => s.HealthStatus == EmailHealthStatus.Degraded),
                Unhealthy     = g.Count(s => s.HealthStatus == EmailHealthStatus.Unavailable),
                NeverValidated= g.Count(s => s.HealthStatus == EmailHealthStatus.Unknown),
            })
            .FirstOrDefaultAsync(ct);

        // ── Runs ──────────────────────────────────────────────────────────────
        var runQ = _db.EmailIngestionRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        if (query.SourceId.HasValue)  runQ = runQ.Where(r => r.EmailSourceId == query.SourceId.Value);
        if (query.RunStatus.HasValue) runQ = runQ.Where(r => r.Status == query.RunStatus.Value);
        if (fromUtc.HasValue)         runQ = runQ.Where(r => r.StartedAt >= fromUtc.Value);
        if (toUtc.HasValue)           runQ = runQ.Where(r => r.StartedAt <= toUtc.Value);
        if (query.Provider.HasValue)
        {
            var sourceIds = await _db.EmailSources
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.ProviderType == query.Provider.Value)
                .Select(s => s.Id)
                .ToListAsync(ct);
            runQ = runQ.Where(r => sourceIds.Contains(r.EmailSourceId));
        }

        var runCounts = await runQ
            .GroupBy(r => 1)
            .Select(g => new
            {
                Queued              = g.Count(r => r.Status == IngestionRunStatus.Queued),
                Active              = g.Count(r => r.Status == IngestionRunStatus.Running),
                Completed           = g.Count(r => r.Status == IngestionRunStatus.Completed),
                CompletedWithErrors = g.Count(r => r.Status == IngestionRunStatus.CompletedWithErrors),
                Failed              = g.Count(r => r.Status == IngestionRunStatus.Failed),
                MessagesImported    = g.Sum(r => r.MessagesImported),
                Duplicates          = g.Sum(r => r.MessagesDuplicated),
                MessageFailures     = g.Sum(r => r.MessagesFailed),
                AttachmentsDispatched = g.Sum(r => r.AttachmentsDispatched),
                AttachmentFailures  = g.Sum(r => r.AttachmentsFailed),
                AvgDuration         = g.Where(r => r.DurationMs.HasValue)
                                       .Average(r => (double?)r.DurationMs) ?? 0.0,
                RetryCount          = g.Sum(r => r.RetryCount),
            })
            .FirstOrDefaultAsync(ct);

        // Currently syncing — active run sources
        var currentlySyncing = await _db.EmailSourceSyncLocks
            .AsNoTracking()
            .CountAsync(l => l.TenantId == tenantId && l.ExpiresAt > DateTime.UtcNow, ct);

        // ── Alerts ────────────────────────────────────────────────────────────
        var alertCounts = await _db.EmailOperationalAlerts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Status == EmailAlertStatus.Open)
            .GroupBy(a => a.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var criticalAlerts     = alertCounts.FirstOrDefault(a => a.Severity == EmailAlertSeverity.Critical)?.Count ?? 0;
        var warningAlerts      = alertCounts.FirstOrDefault(a => a.Severity == EmailAlertSeverity.Warning)?.Count ?? 0;
        var infoAlerts         = alertCounts.FirstOrDefault(a => a.Severity == EmailAlertSeverity.Informational)?.Count ?? 0;

        return new OperationsSummaryResult(
            TotalSources:              sourceCounts?.Total ?? 0,
            EnabledSources:            sourceCounts?.Enabled ?? 0,
            HealthySources:            sourceCounts?.Healthy ?? 0,
            DegradedSources:           sourceCounts?.Degraded ?? 0,
            UnhealthySources:          sourceCounts?.Unhealthy ?? 0,
            NeverValidatedSources:     sourceCounts?.NeverValidated ?? 0,
            CurrentlySyncingSources:   currentlySyncing,
            QueuedRuns:                runCounts?.Queued ?? 0,
            ActiveRuns:                runCounts?.Active ?? 0,
            CompletedRuns:             runCounts?.Completed ?? 0,
            CompletedWithErrorsRuns:   runCounts?.CompletedWithErrors ?? 0,
            FailedRuns:                runCounts?.Failed ?? 0,
            MessagesImported:          runCounts?.MessagesImported ?? 0,
            DuplicateMessages:         runCounts?.Duplicates ?? 0,
            MessageFailures:           runCounts?.MessageFailures ?? 0,
            AttachmentsDispatched:     runCounts?.AttachmentsDispatched ?? 0,
            AttachmentFailures:        runCounts?.AttachmentFailures ?? 0,
            AverageSyncDurationMs:     runCounts?.AvgDuration ?? 0.0,
            ProviderThrottlingIncidents: 0,
            RetryCount:                runCounts?.RetryCount ?? 0,
            LockContentionIncidents:   0,
            CursorResets:              0,
            OpenAlertsCritical:        criticalAlerts,
            OpenAlertsWarning:         warningAlerts,
            OpenAlertsInformational:   infoAlerts,
            GeneratedAt:               DateTime.UtcNow);
    }
}
