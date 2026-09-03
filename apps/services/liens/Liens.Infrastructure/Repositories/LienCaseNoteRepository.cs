using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienCaseNoteRepository : ILienCaseNoteRepository
{
    private readonly LiensDbContext _db;

    public LienCaseNoteRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<List<LienCaseNote>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        return await _db.LienCaseNotes
            .Where(n => n.TenantId == tenantId && n.CaseId == caseId && !n.IsDeleted)
            .OrderBy(n => n.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<CaseNoteReportRow>> GetTrackingByCaseIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken ct = default)
    {
        if (caseIds.Count == 0)
            return [];

        return await _db.LienCaseNotes
            .AsNoTracking()
            .Where(note => note.TenantId == tenantId &&
                           caseIds.Contains(note.CaseId) &&
                           !note.IsDeleted &&
                           (note.Category == CaseNoteCategory.General ||
                            note.Category == CaseNoteCategory.FollowUp))
            .OrderBy(note => note.CaseId)
            .ThenByDescending(note => note.CreatedAtUtc)
            .ThenByDescending(note => note.Id)
            .Select(note => new CaseNoteReportRow(
                note.Id,
                note.CaseId,
                note.Content,
                note.Category,
                note.CreatedAtUtc,
                note.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<List<CaseNoteReportRow>> GetLatestCaseUpdatesByCaseIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken ct = default)
    {
        if (caseIds.Count == 0)
            return [];

        var noteUpdates = await _db.LienCaseNotes
            .AsNoTracking()
            .Where(note => note.TenantId == tenantId &&
                           caseIds.Contains(note.CaseId) &&
                           !note.IsDeleted &&
                           (note.Category == CaseNoteCategory.Internal ||
                            note.Category == CaseNoteCategory.CaseCreated))
            .GroupBy(note => note.CaseId)
            .Select(group => group
                .OrderByDescending(note => note.UpdatedAtUtc ?? note.CreatedAtUtc)
                .ThenByDescending(note => note.Id)
                .Select(note => new CaseNoteReportRow(
                    note.Id,
                    note.CaseId,
                    note.Content,
                    note.Category,
                    note.CreatedAtUtc,
                    note.UpdatedAtUtc))
                .First())
            .ToListAsync(ct);

        var nativeUpdates = await _db.CaseUpdateHistories
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && caseIds.Contains(item.CaseId))
            .GroupBy(item => item.CaseId)
            .Select(group => group
                .OrderByDescending(item => item.OccurredAtUtc)
                .ThenByDescending(item => item.Id)
                .Select(item => new CaseNoteReportRow(
                    item.Id,
                    item.CaseId,
                    item.Description,
                    item.Action,
                    item.OccurredAtUtc,
                    item.OccurredAtUtc))
                .First())
            .ToListAsync(ct);

        return noteUpdates
            .Concat(nativeUpdates)
            .GroupBy(item => item.CaseId)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
                .ThenByDescending(item => item.Id)
                .First())
            .ToList();
    }

    public async Task<List<CaseNoteReportRow>> GetLatestFeedByCaseIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken ct = default)
    {
        if (caseIds.Count == 0)
            return [];

        return await BuildLatestFeedReportQuery(tenantId, caseIds).ToListAsync(ct);
    }

    internal IQueryable<CaseNoteReportRow> BuildLatestFeedReportQuery(
        Guid tenantId,
        IReadOnlyCollection<Guid> caseIds)
    {
        return _db.LienCaseNotes
            .AsNoTracking()
            .Where(note => note.TenantId == tenantId &&
                           caseIds.Contains(note.CaseId) &&
                           !note.IsDeleted &&
                           note.Category == CaseNoteCategory.Feed &&
                           note.Content.Trim() != string.Empty)
            .GroupBy(note => note.CaseId)
            .Select(group => group
                .OrderByDescending(note => note.CreatedAtUtc)
                .ThenByDescending(note => note.Id)
                .Select(note => new CaseNoteReportRow(
                    note.Id,
                    note.CaseId,
                    note.Content,
                    note.Category,
                    note.CreatedAtUtc,
                    note.UpdatedAtUtc))
                .First());
    }

    public async Task<List<LienCaseNote>> GetByCaseIdIncludingDeletedAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        return await _db.LienCaseNotes
            .Where(n => n.TenantId == tenantId && n.CaseId == caseId)
            .OrderBy(n => n.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<LienCaseNote?> GetByIdAsync(Guid tenantId, Guid noteId, CancellationToken ct = default)
    {
        return await _db.LienCaseNotes
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == noteId, ct);
    }

    public async Task AddAsync(LienCaseNote note, CancellationToken ct = default)
    {
        await _db.LienCaseNotes.AddAsync(note, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienCaseNote note, CancellationToken ct = default)
    {
        _db.LienCaseNotes.Update(note);
        await _db.SaveChangesAsync(ct);
    }
}
