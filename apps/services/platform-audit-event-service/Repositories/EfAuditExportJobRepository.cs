using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="AuditExportJob"/>.
///
/// Export jobs have a mutable lifecycle. Write operations (Create, Update) open
/// short-lived contexts to keep transaction scope minimal and avoid stale-tracking
/// issues between the create and update calls from the export worker.
/// </summary>
public sealed class EfAuditExportJobRepository : IAuditExportJobRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfAuditExportJobRepository> _logger;

    public EfAuditExportJobRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfAuditExportJobRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<AuditExportJob> CreateAsync(
        AuditExportJob job,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.AuditExportJobs.Add(job);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AuditExportJob created: ExportId={ExportId} RequestedBy={RequestedBy} Format={Format}",
            job.ExportId, job.RequestedBy, job.Format);

        return job;
    }

    public async Task<AuditExportJob?> GetByExportIdAsync(
        Guid exportId,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.ExportId == exportId, ct);
    }

    public async Task<AuditExportJob> UpdateAsync(
        AuditExportJob job,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Attach the detached entity and mark mutable fields as modified.
        // This pattern avoids a redundant SELECT before updating lifecycle fields.
        db.AuditExportJobs.Attach(job);
        var entry = db.Entry(job);
        entry.Property(j => j.Status).IsModified         = true;
        entry.Property(j => j.FilePath).IsModified       = true;
        entry.Property(j => j.ErrorMessage).IsModified   = true;
        entry.Property(j => j.CompletedAtUtc).IsModified = true;

        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "AuditExportJob updated: ExportId={ExportId} Status={Status}",
            job.ExportId, job.Status);

        return job;
    }

    public async Task<PagedResult<AuditExportJob>> ListByRequesterAsync(
        string requestedBy,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AuditExportJobs
            .AsNoTracking()
            .Where(j => j.RequestedBy == requestedBy)
            .OrderByDescending(j => j.CreatedAtUtc);

        var total = await query.CountAsync(ct);   // int — matches PagedResult<T>.TotalCount
        pageSize  = Math.Max(1, Math.Min(pageSize, 200));
        page      = Math.Max(1, page);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditExportJob>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<IReadOnlyList<AuditExportJob>> ListActiveAsync(
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditExportJobs
            .AsNoTracking()
            .Where(j => j.Status == ExportStatus.Pending || j.Status == ExportStatus.Processing)
            .OrderBy(j => j.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
