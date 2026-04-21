using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskLinkedEntityRepository
{
    System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntity>> GetByTaskAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntity>> GetByEntityAsync(
        Guid   tenantId,
        string entityType,
        string entityId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskLinkedEntity?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    System.Threading.Tasks.Task AddAsync(
        TaskLinkedEntity entity, CancellationToken ct = default);

    void Remove(TaskLinkedEntity entity);
}
