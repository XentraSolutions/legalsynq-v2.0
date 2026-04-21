using BuildingBlocks.Domain;
using TaskStatus   = Task.Domain.Enums.TaskStatus;
using TaskPriority = Task.Domain.Enums.TaskPriority;
using TaskScope    = Task.Domain.Enums.TaskScope;

namespace Task.Domain.Entities;

public class PlatformTask : AuditableEntity
{
    public Guid    Id                 { get; private set; }
    public Guid    TenantId           { get; private set; }

    public string  Title              { get; private set; } = string.Empty;
    public string? Description        { get; private set; }

    public string  Status             { get; private set; } = TaskStatus.Open;
    public string  Priority           { get; private set; } = TaskPriority.Medium;

    public Guid?   AssignedUserId     { get; private set; }

    public string  Scope              { get; private set; } = TaskScope.General;
    public string? SourceProductCode  { get; private set; }
    public string? SourceEntityType   { get; private set; }
    public Guid?   SourceEntityId     { get; private set; }

    /// <summary>
    /// Optional reference to the current execution stage from <see cref="TaskStageConfig"/>.
    /// Null if no stage is assigned or stages are not configured for this tenant/product.
    /// </summary>
    public Guid?   CurrentStageId     { get; private set; }

    public DateTime? DueAt            { get; private set; }
    public DateTime? CompletedAt      { get; private set; }
    public Guid?   ClosedByUserId     { get; private set; }

    private PlatformTask() { }

    public static PlatformTask Create(
        Guid      tenantId,
        string    title,
        Guid      createdByUserId,
        string?   description       = null,
        string?   priority          = null,
        string?   scope             = null,
        Guid?     assignedUserId    = null,
        string?   sourceProductCode = null,
        string?   sourceEntityType  = null,
        Guid?     sourceEntityId    = null,
        DateTime? dueAt             = null,
        Guid?     currentStageId    = null)
    {
        if (tenantId == Guid.Empty)        throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var effectivePriority = priority ?? TaskPriority.Medium;
        if (!TaskPriority.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(priority));

        var effectiveScope = scope ?? TaskScope.General;
        if (!TaskScope.All.Contains(effectiveScope))
            throw new ArgumentException($"Invalid scope: '{effectiveScope}'.", nameof(scope));

        if (effectiveScope == TaskScope.Product && string.IsNullOrWhiteSpace(sourceProductCode))
            throw new ArgumentException("sourceProductCode is required for PRODUCT-scoped tasks.", nameof(sourceProductCode));

        var now = DateTime.UtcNow;
        return new PlatformTask
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            Title             = title.Trim(),
            Description       = description?.Trim(),
            Status            = TaskStatus.Open,
            Priority          = effectivePriority,
            Scope             = effectiveScope,
            AssignedUserId    = assignedUserId,
            SourceProductCode = sourceProductCode?.Trim().ToUpperInvariant(),
            SourceEntityType  = sourceEntityType?.Trim(),
            SourceEntityId    = sourceEntityId,
            DueAt             = dueAt,
            CurrentStageId    = currentStageId,
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = now,
            UpdatedAtUtc      = now,
        };
    }

    public void Update(
        string    title,
        Guid      updatedByUserId,
        string?   description    = null,
        string?   priority       = null,
        Guid?     assignedUserId = null,
        DateTime? dueAt          = null,
        Guid?     currentStageId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var effectivePriority = priority ?? Priority;
        if (!TaskPriority.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(priority));

        Title           = title.Trim();
        Description     = description?.Trim();
        Priority        = effectivePriority;
        AssignedUserId  = assignedUserId;
        DueAt           = dueAt;
        CurrentStageId  = currentStageId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId)
    {
        if (!TaskStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid status: '{newStatus}'.", nameof(newStatus));
        if (TaskStatus.IsTerminal(Status))
            throw new InvalidOperationException($"Cannot transition from terminal status '{Status}'.");

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (newStatus == TaskStatus.Completed)
        {
            CompletedAt    = DateTime.UtcNow;
            ClosedByUserId = updatedByUserId;
        }
        else if (newStatus == TaskStatus.Cancelled)
        {
            ClosedByUserId = updatedByUserId;
        }
    }

    /// <summary>
    /// Assigns or unassigns the task. Returns the <see cref="AssignmentChangeKind"/>
    /// so the caller can write the appropriate history action (ASSIGNED / REASSIGNED / UNASSIGNED).
    /// Returns <see cref="AssignmentChangeKind.NoOp"/> when the requested value equals the current one.
    /// </summary>
    public AssignmentChangeKind Assign(Guid? userId, Guid updatedByUserId)
    {
        var previous = AssignedUserId;

        if (previous == userId)
            return AssignmentChangeKind.NoOp;

        AssignedUserId  = userId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (userId is null)
            return AssignmentChangeKind.Unassigned;

        return previous is null
            ? AssignmentChangeKind.Assigned
            : AssignmentChangeKind.Reassigned;
    }

    /// <summary>Sets or clears the current execution stage.</summary>
    public void SetStage(Guid? stageId, Guid updatedByUserId)
    {
        CurrentStageId  = stageId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}

/// <summary>Describes what kind of assignment change occurred.</summary>
public enum AssignmentChangeKind
{
    NoOp       = 0,
    Assigned   = 1,
    Reassigned = 2,
    Unassigned = 3,
}
