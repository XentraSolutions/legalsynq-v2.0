using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed dead-letter queue store.
///
/// Replaces <see cref="InMemoryAutomationDeadLetterStore"/> for production use.
/// Persistence is per-row with optimistic concurrency via RowVersion.
///
/// Safety:
/// - Only safe_error_summary (bounded VARCHAR) stored; no raw stack traces.
/// - All mutations check tenant isolation.
/// - AcquireForRetry uses optimistic concurrency to prevent double-retry.
/// </summary>
internal sealed class EfAutomationDeadLetterStore : IAutomationDeadLetterStore
{
    private readonly IDbContextFactory<XeniaDbContext> _contextFactory;
    private readonly ILogger<EfAutomationDeadLetterStore> _logger;

    public EfAutomationDeadLetterStore(
        IDbContextFactory<XeniaDbContext> contextFactory,
        ILogger<EfAutomationDeadLetterStore> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<AutomationDeadLetterEntry> CreateAsync(
        AutomationDeadLetterEntry entry, CancellationToken ct = default)
    {
        var record = AutomationDeadLetterRecord.Create(
            tenantId:        ToDbTenantId(entry.TenantId),
            automationKey:   entry.AutomationKey,
            automationVersion: entry.AutomationVersion,
            triggerType:     entry.TriggerType,
            failureCategory: entry.FailureCategory,
            safeErrorSummary: entry.SafeErrorSummary,
            retryCount:      entry.RetryCount,
            executionId:     entry.ExecutionId == Guid.Empty ? null : entry.ExecutionId,
            correlationId:   entry.CorrelationId is null ? null
                                : Guid.TryParse(entry.CorrelationId, out var cid) ? cid : null);

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        ctx.AutomationDeadLetters.Add(record);
        await ctx.SaveChangesAsync(ct);
        return ToEntry(record);
    }

    public async Task<AutomationDeadLetterEntry?> GetAsync(
        Guid id, Guid? tenantId, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var record = await ctx.AutomationDeadLetters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (record is null) return null;
        if (tenantId.HasValue && record.TenantId != ToDbTenantId(tenantId)) return null;
        return ToEntry(record);
    }

    public async Task<IReadOnlyList<AutomationDeadLetterEntry>> ListAsync(
        string? automationKey, Guid? tenantId, AutomationDeadLetterStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        IQueryable<AutomationDeadLetterRecord> query = ctx.AutomationDeadLetters.AsNoTracking();

        if (tenantId.HasValue)
            query = query.Where(r => r.TenantId == ToDbTenantId(tenantId));

        if (automationKey is not null)
            query = query.Where(r => r.AutomationKey == automationKey);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var records = await query
            .OrderByDescending(r => r.FirstFailedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return records.ConvertAll(ToEntry);
    }

    public async Task<bool> RetryAsync(
        Guid id, Guid? tenantId, DateTime nextEligibleAt, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var record = await ctx.AutomationDeadLetters.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (record is null) return false;
        if (tenantId.HasValue && record.TenantId != ToDbTenantId(tenantId)) return false;

        try
        {
            record.AcquireForRetry(nextEligibleAt);
            await ctx.SaveChangesAsync(ct);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot retry dead letter {Id}: invalid status transition", id);
            return false;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict on retry for dead letter {Id}", id);
            return false;
        }
    }

    public async Task<bool> AbandonAsync(
        Guid id, Guid? tenantId, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var record = await ctx.AutomationDeadLetters.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (record is null) return false;
        if (tenantId.HasValue && record.TenantId != ToDbTenantId(tenantId)) return false;

        record.MarkAbandoned();
        await ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ResolveAsync(
        Guid id, Guid? tenantId, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var record = await ctx.AutomationDeadLetters.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (record is null) return false;
        if (tenantId.HasValue && record.TenantId != ToDbTenantId(tenantId)) return false;

        record.MarkResolved();
        await ctx.SaveChangesAsync(ct);
        return true;
    }

    private static Guid ToDbTenantId(Guid? tenantId) => tenantId ?? Guid.Empty;

    private static AutomationDeadLetterEntry ToEntry(AutomationDeadLetterRecord r)
    {
        var tenantId = r.TenantId == Guid.Empty ? (Guid?)null : r.TenantId;
        return AutomationDeadLetterEntry.Reconstitute(
            id:                 r.Id,
            tenantId:           tenantId,
            automationKey:      r.AutomationKey,
            automationVersion:  r.AutomationVersion,
            executionId:        r.ExecutionId ?? Guid.Empty,
            triggerType:        r.TriggerType,
            failureCategory:    r.FailureCategory,
            safeErrorSummary:   r.SafeErrorSummary ?? string.Empty,
            retryCount:         r.RetryCount,
            firstFailedAt:      r.FirstFailedAt,
            lastFailedAt:       r.LastFailedAt,
            nextEligibleRetryAt: r.NextEligibleRetryAt,
            status:             r.Status,
            correlationId:      r.CorrelationId?.ToString(),
            createdAt:          r.CreatedAtUtc,
            updatedAt:          r.UpdatedAtUtc);
    }
}
