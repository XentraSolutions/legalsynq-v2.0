using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskRepository
{
    System.Threading.Tasks.Task<PlatformTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<(IReadOnlyList<PlatformTask> Items, int Total)> SearchAsync(
        Guid    tenantId,
        string? search            = null,
        string? status            = null,
        string? priority          = null,
        string? scope             = null,
        Guid?   assignedUserId    = null,
        string? sourceProductCode = null,
        int     page              = 1,
        int     pageSize          = 50,
        CancellationToken ct      = default);
    System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetByAssignedUserAsync(
        Guid   tenantId,
        Guid   userId,
        CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(PlatformTask task, CancellationToken ct = default);
}
