using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienTaskRepository
{
    Task<LienTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<LienTask> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        string? search,
        string? status,
        string? priority,
        Guid? assignedUserId,
        Guid? caseId,
        Guid? lienId,
        Guid? workflowStageId,
        string? assignmentScope,
        Guid? currentUserId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<List<LienTaskLienLink>> GetLienLinksForTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<List<LienTaskLienLink>> GetTaskLinksForLienAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task AddAsync(LienTask entity, CancellationToken ct = default);
    Task UpdateAsync(LienTask entity, CancellationToken ct = default);
    Task AddLienLinksAsync(IEnumerable<LienTaskLienLink> links, CancellationToken ct = default);
    Task RemoveLienLinksAsync(Guid taskId, CancellationToken ct = default);

    Task<bool> HasOpenTaskForRuleAsync(Guid tenantId, Guid ruleId, Guid? caseId, Guid? lienId, CancellationToken ct = default);
    Task<bool> HasOpenTaskForTemplateAsync(Guid tenantId, Guid templateId, Guid? caseId, Guid? lienId, CancellationToken ct = default);
    Task AddGeneratedMetadataAsync(LienGeneratedTaskMetadata metadata, CancellationToken ct = default);
}
