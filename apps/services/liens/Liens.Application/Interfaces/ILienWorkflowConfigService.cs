using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienWorkflowConfigService
{
    Task<WorkflowConfigResponse?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<WorkflowConfigResponse> CreateAsync(Guid tenantId, Guid actingUserId, CreateWorkflowConfigRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> UpdateAsync(Guid tenantId, Guid id, Guid actingUserId, UpdateWorkflowConfigRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> AddStageAsync(Guid tenantId, Guid id, Guid actingUserId, AddWorkflowStageRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> UpdateStageAsync(Guid tenantId, Guid id, Guid stageId, Guid actingUserId, UpdateWorkflowStageRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> RemoveStageAsync(Guid tenantId, Guid id, Guid stageId, Guid actingUserId, CancellationToken ct = default);

    Task<WorkflowConfigResponse> ReorderStagesAsync(Guid tenantId, Guid id, Guid actingUserId, ReorderStagesRequest request, CancellationToken ct = default);
}
