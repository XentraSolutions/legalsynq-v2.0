using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly TasksDbContext _db;
    public TaskRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<PlatformTask?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.Tasks
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public async System.Threading.Tasks.Task<(IReadOnlyList<PlatformTask> Items, int Total)> SearchAsync(
        Guid      tenantId,
        string?   search            = null,
        string?   status            = null,
        string?   priority          = null,
        string?   scope             = null,
        Guid?     assignedUserId    = null,
        string?   sourceProductCode = null,
        int       page              = 1,
        int       pageSize          = 50,
        CancellationToken ct        = default)
    {
        var q = _db.Tasks.Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(t => t.Title.Contains(search) ||
                              (t.Description != null && t.Description.Contains(search)));

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(priority))
            q = q.Where(t => t.Priority == priority.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(scope))
            q = q.Where(t => t.Scope == scope.ToUpperInvariant());

        if (assignedUserId.HasValue)
            q = q.Where(t => t.AssignedUserId == assignedUserId.Value);

        if (!string.IsNullOrWhiteSpace(sourceProductCode))
            q = q.Where(t => t.SourceProductCode == sourceProductCode.ToUpperInvariant());

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetByAssignedUserAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.Tasks
            .Where(t => t.TenantId == tenantId && t.AssignedUserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddAsync(PlatformTask task, CancellationToken ct = default)
        => await _db.Tasks.AddAsync(task, ct);
}
