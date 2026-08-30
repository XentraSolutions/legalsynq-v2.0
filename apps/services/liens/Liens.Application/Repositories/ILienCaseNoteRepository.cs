using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienCaseNoteRepository
{
    Task<List<LienCaseNote>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<List<CaseNoteReportRow>> GetTrackingByCaseIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken ct = default);
    Task<List<CaseNoteReportRow>> GetLatestCaseUpdatesByCaseIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken ct = default);
    Task<List<CaseNoteReportRow>> GetLatestFeedByCaseIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken ct = default);
    Task<List<LienCaseNote>> GetByCaseIdIncludingDeletedAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<LienCaseNote?> GetByIdAsync(Guid tenantId, Guid noteId, CancellationToken ct = default);
    Task AddAsync(LienCaseNote note, CancellationToken ct = default);
    Task UpdateAsync(LienCaseNote note, CancellationToken ct = default);
}

public sealed record CaseNoteReportRow(
    Guid Id,
    Guid CaseId,
    string Content,
    string Category,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
