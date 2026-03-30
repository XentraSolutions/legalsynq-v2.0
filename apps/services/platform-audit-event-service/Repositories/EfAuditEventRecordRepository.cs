using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;
using AuditRecordQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="AuditEventRecord"/>.
///
/// Append-only contract: no UPDATE or DELETE operations are exposed.
/// All queries use AsNoTracking() for read performance.
/// Writes use short-lived DbContext instances from the factory to keep
/// the transaction scope minimal.
/// </summary>
public sealed class EfAuditEventRecordRepository : IAuditEventRecordRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfAuditEventRecordRepository> _logger;

    public EfAuditEventRecordRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfAuditEventRecordRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<AuditEventRecord> AppendAsync(
        AuditEventRecord record,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.AuditEventRecords.Add(record);
        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "AuditEventRecord persisted: AuditId={AuditId} TenantId={TenantId}",
            record.AuditId, record.TenantId);

        return record;
    }

    public async Task<AuditEventRecord?> GetByAuditIdAsync(
        Guid auditId,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEventRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.AuditId == auditId, ct);
    }

    public async Task<bool> ExistsIdempotencyKeyAsync(
        string? key,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEventRecords
            .AsNoTracking()
            .AnyAsync(r => r.IdempotencyKey == key, ct);
    }

    public async Task<PagedResult<AuditEventRecord>> QueryAsync(
        AuditRecordQueryRequest q,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AuditEventRecords.AsNoTracking();

        // ── Scope filters ──────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.TenantId))
            query = query.Where(r => r.TenantId == q.TenantId);

        if (!string.IsNullOrWhiteSpace(q.OrganizationId))
            query = query.Where(r => r.OrganizationId == q.OrganizationId);

        // ── Classification filters ─────────────────────────────────────────────
        if (q.Category.HasValue)
            query = query.Where(r => r.EventCategory == q.Category.Value);

        if (q.MinSeverity.HasValue)
            query = query.Where(r => r.Severity >= q.MinSeverity.Value);

        if (q.MaxSeverity.HasValue)
            query = query.Where(r => r.Severity <= q.MaxSeverity.Value);

        if (q.EventTypes is { Count: > 0 })
            query = query.Where(r => q.EventTypes.Contains(r.EventType));

        if (!string.IsNullOrWhiteSpace(q.SourceSystem))
            query = query.Where(r => r.SourceSystem == q.SourceSystem);

        if (!string.IsNullOrWhiteSpace(q.SourceService))
            query = query.Where(r => r.SourceService == q.SourceService);

        // ── Actor / identity filters ───────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.ActorId))
            query = query.Where(r => r.ActorId == q.ActorId);

        if (q.ActorType.HasValue)
            query = query.Where(r => r.ActorType == q.ActorType.Value);

        // ── Entity / resource filters ──────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.EntityType))
            query = query.Where(r => r.EntityType == q.EntityType);

        if (!string.IsNullOrWhiteSpace(q.EntityId))
            query = query.Where(r => r.EntityId == q.EntityId);

        // ── Correlation filters ────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.CorrelationId))
            query = query.Where(r => r.CorrelationId == q.CorrelationId);

        if (!string.IsNullOrWhiteSpace(q.SessionId))
            query = query.Where(r => r.SessionId == q.SessionId);

        // ── Time range ─────────────────────────────────────────────────────────
        if (q.From.HasValue)
            query = query.Where(r => r.OccurredAtUtc >= q.From.Value);

        if (q.To.HasValue)
            query = query.Where(r => r.OccurredAtUtc < q.To.Value);

        // ── Visibility ─────────────────────────────────────────────────────────
        if (q.MaxVisibility.HasValue)
            query = query.Where(r => r.VisibilityScope <= q.MaxVisibility.Value);

        // ── Text search ────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.DescriptionContains))
            query = query.Where(r => r.Description.Contains(q.DescriptionContains));

        // ── Sorting ────────────────────────────────────────────────────────────
        var desc = q.SortDescending;
        query = q.SortBy?.ToLowerInvariant() switch
        {
            "recordedat" or "recordedatutc" =>
                desc ? query.OrderByDescending(r => r.RecordedAtUtc)
                     : query.OrderBy(r => r.RecordedAtUtc),
            "severity" =>
                desc ? query.OrderByDescending(r => r.Severity)
                     : query.OrderBy(r => r.Severity),
            "sourcesystem" =>
                desc ? query.OrderByDescending(r => r.SourceSystem)
                     : query.OrderBy(r => r.SourceSystem),
            _ =>
                desc ? query.OrderByDescending(r => r.OccurredAtUtc)
                     : query.OrderBy(r => r.OccurredAtUtc),
        };

        // ── Pagination ─────────────────────────────────────────────────────────
        var total    = await query.CountAsync(ct);   // int — matches PagedResult<T>.TotalCount
        var pageSize = Math.Max(1, Math.Min(q.PageSize, 500));
        var page     = Math.Max(1, q.Page);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditEventRecord>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEventRecords.LongCountAsync(ct);
    }

    public async Task<AuditEventRecord?> GetLatestInChainAsync(
        string? tenantId,
        string sourceSystem,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AuditEventRecords
            .AsNoTracking()
            .Where(r => r.SourceSystem == sourceSystem);

        if (tenantId is not null)
            query = query.Where(r => r.TenantId == tenantId);

        return await query
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
    }
}
