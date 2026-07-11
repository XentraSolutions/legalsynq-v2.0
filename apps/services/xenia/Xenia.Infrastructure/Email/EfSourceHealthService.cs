using Microsoft.EntityFrameworkCore;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Per-source operational health snapshots.
/// Returns safe summaries — never raw cursors, tokens, or credentials.
/// </summary>
internal sealed class EfSourceHealthService : ISourceHealthService
{
    private readonly XeniaDbContext _db;

    public EfSourceHealthService(XeniaDbContext db) => _db = db;

    public async Task<IReadOnlyList<SourceHealthSnapshot>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var sources = await _db.EmailSources
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .ToListAsync(ct);

        var result = new List<SourceHealthSnapshot>(sources.Count);
        foreach (var source in sources)
            result.Add(await BuildSnapshotAsync(tenantId, source, ct));

        return result;
    }

    public async Task<SourceHealthSnapshot?> GetAsync(Guid tenantId, Guid sourceId, CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sourceId && !s.IsDeleted, ct);

        return source is null ? null : await BuildSnapshotAsync(tenantId, source, ct);
    }

    private async Task<SourceHealthSnapshot> BuildSnapshotAsync(
        Guid tenantId, EmailSource source, CancellationToken ct)
    {
        // Last validation
        var lastValidation = await _db.EmailValidationHistory
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.EmailSourceId == source.Id)
            .OrderByDescending(v => v.StartedAt)
            .FirstOrDefaultAsync(ct);

        var lastSuccessfulValidation = await _db.EmailValidationHistory
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.EmailSourceId == source.Id &&
                        v.Result == EmailValidationResult.Success)
            .OrderByDescending(v => v.StartedAt)
            .FirstOrDefaultAsync(ct);

        // Sync state
        var syncState = await _db.EmailSyncStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EmailSourceId == source.Id, ct);

        // Active run
        var activeRun = await _db.EmailIngestionRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.EmailSourceId == source.Id &&
                        r.Status == IngestionRunStatus.Running)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);

        // Lock state
        var lockRow = await _db.EmailSourceSyncLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.EmailSourceId == source.Id, ct);

        var isLocked     = lockRow is not null && !lockRow.IsExpired;
        var lockOwnerId  = isLocked ? SafeOwnerId(lockRow!.LeaseOwnerId) : null;
        var lockExpires  = isLocked ? lockRow!.ExpiresAt : null;

        // Run counters
        var runAgg = await _db.EmailIngestionRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.EmailSourceId == source.Id)
            .GroupBy(r => 1)
            .Select(g => new
            {
                LastAttempt       = g.Max(r => (DateTime?)r.StartedAt),
                LastSuccess       = g.Where(r => r.Status == IngestionRunStatus.Completed ||
                                                 r.Status == IngestionRunStatus.CompletedWithErrors)
                                    .Max(r => (DateTime?)r.StartedAt),
                TotalImported     = g.Sum(r => r.MessagesImported),
                TotalDuplicates   = g.Sum(r => r.MessagesDuplicated),
                AttachFailures    = g.Sum(r => r.AttachmentsFailed),
                ConsecutiveFails  = 0, // simplified
                LastSafeError     = g.OrderByDescending(r => r.StartedAt)
                                     .Select(r => r.SafeErrorSummary)
                                     .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        // Open alerts
        var alertCount = await _db.EmailOperationalAlerts
            .AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && a.EmailSourceId == source.Id &&
                             a.Status == EmailAlertStatus.Open, ct);

        return new SourceHealthSnapshot(
            SourceId:                  source.Id,
            SourceName:                source.DisplayName,
            Provider:                  source.ProviderType,
            IsEnabled:                 source.Status != EmailSourceStatus.Disabled,
            ModuleEffectivelyEnabled:  source.Status != EmailSourceStatus.Disabled,
            ConnectionState:           source.Status,
            ValidationState:           lastValidation is null
                ? null
                : lastValidation.Result == EmailValidationResult.Success
                    ? EmailValidationStatus.Valid
                    : EmailValidationStatus.Invalid,
            HealthState:               source.HealthStatus,
            LastValidationAt:          lastValidation?.StartedAt,
            LastSuccessfulValidationAt: lastSuccessfulValidation?.StartedAt,
            LastSyncAttemptAt:         runAgg?.LastAttempt,
            LastSuccessfulSyncAt:      runAgg?.LastSuccess,
            CurrentRunId:              activeRun == Guid.Empty ? null : activeRun,
            ConsecutiveFailureCount:   syncState?.ConsecutiveFailureCount ?? 0,
            NextRetryAt:               null,
            IsLocked:                  isLocked,
            LockOwnerSafeId:           lockOwnerId,
            LockExpiresAt:             lockExpires,
            CursorType:                syncState?.CursorType,
            SafeCursorSummary:         syncState?.SafeCursorSummary,
            InitialSyncComplete:       syncState?.InitialSyncCompleted ?? false,
            TotalMessagesImported:     runAgg?.TotalImported ?? 0,
            TotalDuplicates:           runAgg?.TotalDuplicates ?? 0,
            TotalAttachmentFailures:   runAgg?.AttachFailures ?? 0,
            LastSafeError:             runAgg?.LastSafeError,
            OpenAlertCount:            alertCount);
    }

    private static string SafeOwnerId(string rawOwnerId)
    {
        if (string.IsNullOrWhiteSpace(rawOwnerId)) return "unknown";
        // Return first 20 chars — enough to identify instance without leaking full hostname
        return rawOwnerId.Length <= 20 ? rawOwnerId : rawOwnerId[..17] + "...";
    }
}
