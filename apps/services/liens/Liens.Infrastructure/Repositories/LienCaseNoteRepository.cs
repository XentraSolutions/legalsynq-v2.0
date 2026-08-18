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

    public async Task<List<LienCaseNote>> GetTrackingByCaseIdsAsync(
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
            .ToListAsync(ct);
    }

    public async Task<List<LienCaseNote>> GetLatestCaseUpdatesByCaseIdsAsync(
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
                           (note.Category == CaseNoteCategory.Internal ||
                            note.Category == CaseNoteCategory.CaseCreated))
            .GroupBy(note => note.CaseId)
            .Select(group => group
                .OrderByDescending(note => note.UpdatedAtUtc ?? note.CreatedAtUtc)
                .ThenByDescending(note => note.Id)
                .First())
            .ToListAsync(ct);
    }

    public async Task<List<LienCaseNote>> GetLatestFeedByCaseIdsAsync(
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
                           note.Category.ToLower() == CaseNoteCategory.Feed &&
                           note.Content.Trim() != string.Empty)
            .GroupBy(note => note.CaseId)
            .Select(group => group
                .OrderByDescending(note => note.CreatedAtUtc)
                .ThenByDescending(note => note.Id)
                .First())
            .ToListAsync(ct);
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
