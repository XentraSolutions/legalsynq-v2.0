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
        string?   search             = null,
        string?   status             = null,
        string?   priority           = null,
        string?   scope              = null,
        Guid?     assignedUserId     = null,
        string?   sourceProductCode   = null,
        Guid?     stageId             = null,
        DateTime? dueBefore           = null,
        DateTime? dueAfter            = null,
        Guid?     workflowInstanceId  = null,
        string?   sourceEntityType    = null,
        Guid?     sourceEntityId      = null,
        string?   linkedEntityType    = null,
        Guid?     linkedEntityId      = null,
        string?   assignmentScope     = null,
        Guid?     currentUserId       = null,
        int       page                = 1,
        int       pageSize            = 50,
        CancellationToken ct          = default)
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

        if (stageId.HasValue)
            q = q.Where(t => t.CurrentStageId == stageId.Value);

        if (dueBefore.HasValue)
            q = q.Where(t => t.DueAt != null && t.DueAt <= dueBefore.Value);

        if (dueAfter.HasValue)
            q = q.Where(t => t.DueAt != null && t.DueAt >= dueAfter.Value);

        if (workflowInstanceId.HasValue)
            q = q.Where(t => t.WorkflowInstanceId == workflowInstanceId.Value);

        if (!string.IsNullOrWhiteSpace(sourceEntityType))
            q = q.Where(t => t.SourceEntityType == sourceEntityType);

        if (sourceEntityId.HasValue)
            q = q.Where(t => t.SourceEntityId == sourceEntityId.Value);

        if (!string.IsNullOrWhiteSpace(linkedEntityType) && linkedEntityId.HasValue)
        {
            var linkedEntityIdStr = linkedEntityId.Value.ToString();
            var linkedTaskIds = await _db.LinkedEntities
                .Where(e => e.EntityType == linkedEntityType && e.EntityId == linkedEntityIdStr)
                .Select(e => e.TaskId)
                .ToListAsync(ct);
            q = q.Where(t => linkedTaskIds.Contains(t.Id));
        }

        if (!string.IsNullOrWhiteSpace(assignmentScope) && currentUserId.HasValue)
        {
            if (assignmentScope.Equals("ME", StringComparison.OrdinalIgnoreCase))
                q = q.Where(t => t.AssignedUserId == currentUserId.Value);
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetByAssignedUserAsync(
        Guid      tenantId,
        Guid      userId,
        string?   productCode = null,
        string?   status      = null,
        int       page        = 1,
        int       pageSize    = 200,
        CancellationToken ct  = default)
    {
        var q = _db.Tasks.Where(t => t.TenantId == tenantId && t.AssignedUserId == userId);

        if (!string.IsNullOrWhiteSpace(productCode))
            q = q.Where(t => t.SourceProductCode == productCode.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status.ToUpperInvariant());

        return await q
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetByWorkflowInstanceAsync(
        Guid tenantId, Guid workflowInstanceId, CancellationToken ct = default)
        => await _db.Tasks
            .Where(t => t.TenantId == tenantId && t.WorkflowInstanceId == workflowInstanceId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetBySourceEntityAsync(
        Guid   tenantId,
        string entityType,
        Guid   entityId,
        CancellationToken ct = default)
        => await _db.Tasks
            .Where(t => t.TenantId      == tenantId
                     && t.SourceEntityType == entityType
                     && t.SourceEntityId   == entityId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IReadOnlyList<(string? ProductCode, string Status, int Count)>> GetMyTaskCountsAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.Tasks
            .Where(t => t.TenantId == tenantId && t.AssignedUserId == userId)
            .GroupBy(t => new { t.SourceProductCode, t.Status })
            .Select(g => new
            {
                g.Key.SourceProductCode,
                g.Key.Status,
                Count = g.Count()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => (r.SourceProductCode, r.Status, r.Count))
            .ToList()
            .AsReadOnly();
    }

    public async System.Threading.Tasks.Task AddAsync(PlatformTask task, CancellationToken ct = default)
        => await _db.Tasks.AddAsync(task, ct);
}
