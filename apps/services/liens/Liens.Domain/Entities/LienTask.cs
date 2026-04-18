using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class LienTask : AuditableEntity
{
    public Guid   Id               { get; private set; }
    public Guid   TenantId         { get; private set; }

    public string Title            { get; private set; } = string.Empty;
    public string? Description     { get; private set; }

    public string Status           { get; private set; } = TaskStatuses.New;
    public string Priority         { get; private set; } = TaskPriorities.Medium;

    public Guid?  AssignedUserId   { get; private set; }
    public Guid?  CaseId           { get; private set; }
    public Guid?  WorkflowStageId  { get; private set; }

    public DateTime? DueDate       { get; private set; }
    public DateTime? CompletedAt   { get; private set; }
    public Guid?  ClosedByUserId   { get; private set; }

    private LienTask() { }

    public static LienTask Create(
        Guid tenantId,
        string title,
        Guid createdByUserId,
        string? description = null,
        string? priority = null,
        Guid? assignedUserId = null,
        Guid? caseId = null,
        Guid? workflowStageId = null,
        DateTime? dueDate = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var effectivePriority = priority ?? TaskPriorities.Medium;
        if (!TaskPriorities.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(priority));

        var now = DateTime.UtcNow;
        return new LienTask
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            Title           = title.Trim(),
            Description     = description?.Trim(),
            Status          = TaskStatuses.New,
            Priority        = effectivePriority,
            AssignedUserId  = assignedUserId,
            CaseId          = caseId,
            WorkflowStageId = workflowStageId,
            DueDate         = dueDate,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(
        string title,
        Guid updatedByUserId,
        string? description = null,
        string? priority = null,
        Guid? caseId = null,
        Guid? workflowStageId = null,
        DateTime? dueDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var effectivePriority = priority ?? Priority;
        if (!TaskPriorities.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(priority));

        Title           = title.Trim();
        Description     = description?.Trim();
        Priority        = effectivePriority;
        CaseId          = caseId;
        WorkflowStageId = workflowStageId;
        DueDate         = dueDate;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Assign(Guid? userId, Guid updatedByUserId)
    {
        AssignedUserId  = userId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId)
    {
        if (!TaskStatuses.All.Contains(newStatus))
            throw new ArgumentException($"Invalid task status: '{newStatus}'.", nameof(newStatus));

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (newStatus == TaskStatuses.Completed)
        {
            CompletedAt     = DateTime.UtcNow;
            ClosedByUserId  = updatedByUserId;
        }
        else if (newStatus == TaskStatuses.Cancelled)
        {
            ClosedByUserId  = updatedByUserId;
        }
    }

    public void Complete(Guid updatedByUserId) => TransitionStatus(TaskStatuses.Completed, updatedByUserId);
    public void Cancel(Guid updatedByUserId)   => TransitionStatus(TaskStatuses.Cancelled, updatedByUserId);
}
