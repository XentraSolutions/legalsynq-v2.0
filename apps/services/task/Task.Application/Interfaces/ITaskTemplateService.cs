using Task.Application.DTOs;

namespace Task.Application.Interfaces;

public interface ITaskTemplateService
{
    System.Threading.Tasks.Task<TaskTemplateDto> CreateAsync(Guid tenantId, Guid userId, CreateTaskTemplateRequest request, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskTemplateDto> UpdateAsync(Guid tenantId, Guid id, Guid userId, UpdateTaskTemplateRequest request, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskTemplateDto> ActivateAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskTemplateDto> DeactivateAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IReadOnlyList<TaskTemplateDto>> ListAsync(Guid tenantId, string? sourceProductCode = null, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskTemplateDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskDto> CreateTaskFromTemplateAsync(Guid tenantId, Guid userId, Guid templateId, CreateTaskFromTemplateRequest request, CancellationToken ct = default);
}
