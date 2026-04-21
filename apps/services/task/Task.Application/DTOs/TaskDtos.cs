using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record CreateTaskRequest(
    string    Title,
    string?   Description        = null,
    string?   Priority           = null,
    string?   Scope              = null,
    Guid?     AssignedUserId     = null,
    string?   SourceProductCode  = null,
    string?   SourceEntityType   = null,
    Guid?     SourceEntityId     = null,
    DateTime? DueAt              = null,
    Guid?     WorkflowInstanceId = null,
    string?   WorkflowStepKey    = null);

public record UpdateTaskRequest(
    string    Title,
    string?   Description    = null,
    string?   Priority       = null,
    Guid?     AssignedUserId = null,
    DateTime? DueAt          = null);

public record AssignTaskRequest(Guid? AssignedUserId);

public record TransitionStatusRequest(string Status);

public record AddNoteRequest(string Note);

/// <summary>Full task projection returned by all task endpoints.</summary>
public record TaskDto(
    Guid      Id,
    Guid      TenantId,
    string    Title,
    string?   Description,
    string    Status,
    string    Priority,
    string    Scope,
    Guid?     AssignedUserId,
    string?   SourceProductCode,
    string?   SourceEntityType,
    Guid?     SourceEntityId,
    Guid?     CurrentStageId,
    Guid?     WorkflowInstanceId,
    string?   WorkflowStepKey,
    DateTime? WorkflowLinkageChangedAt,
    DateTime? DueAt,
    DateTime? CompletedAt,
    Guid?     ClosedByUserId,
    Guid?     CreatedByUserId,
    Guid?     UpdatedByUserId,
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc)
{
    public static TaskDto From(PlatformTask t) => new(
        t.Id, t.TenantId, t.Title, t.Description,
        t.Status, t.Priority, t.Scope,
        t.AssignedUserId,
        t.SourceProductCode, t.SourceEntityType, t.SourceEntityId,
        t.CurrentStageId,
        t.WorkflowInstanceId, t.WorkflowStepKey, t.WorkflowLinkageChangedAt,
        t.DueAt, t.CompletedAt, t.ClosedByUserId,
        t.CreatedByUserId, t.UpdatedByUserId,
        t.CreatedAtUtc, t.UpdatedAtUtc);
}

public record TaskNoteDto(
    Guid     Id,
    Guid     TaskId,
    string   Note,
    Guid?    CreatedByUserId,
    DateTime CreatedAtUtc)
{
    public static TaskNoteDto From(TaskNote n) => new(
        n.Id, n.TaskId, n.Note, n.CreatedByUserId, n.CreatedAtUtc);
}

public record TaskHistoryDto(
    Guid     Id,
    Guid     TaskId,
    string   Action,
    string?  Details,
    Guid     PerformedByUserId,
    DateTime CreatedAtUtc)
{
    public static TaskHistoryDto From(TaskHistory h) => new(
        h.Id, h.TaskId, h.Action, h.Details, h.PerformedByUserId, h.CreatedAtUtc);
}

public record TaskListResponse(
    IReadOnlyList<TaskDto> Items,
    int                    Total,
    int                    Page,
    int                    PageSize);

/// <summary>Request to update Flow workflow linkage on a task (admin only).</summary>
public record UpdateWorkflowLinkageRequest(
    Guid?   WorkflowInstanceId,
    string? WorkflowStepKey);

/// <summary>
/// Callback payload sent from Flow (or a background orchestrator) when a workflow step transitions.
/// Identifies all tasks linked to the given WorkflowInstanceId and updates their WorkflowStepKey.
/// Idempotent: no-op when the step key is already set to NewStepKey.
/// </summary>
public record FlowStepCallbackRequest(
    Guid    WorkflowInstanceId,
    string  NewStepKey,
    Guid    TenantId,
    Guid?   UpdatedByUserId = null);

/// <summary>Result returned from the flow-callback endpoint.</summary>
public record FlowCallbackResult(
    int    TasksUpdated,
    int    TasksSkipped,
    Guid   WorkflowInstanceId,
    string NewStepKey);

/// <summary>Lightweight workflow-context projection for a task.</summary>
public record TaskWorkflowContextDto(
    Guid      TaskId,
    Guid?     WorkflowInstanceId,
    string?   WorkflowStepKey,
    DateTime? WorkflowLinkageChangedAt);

/// <summary>Task count summary grouped by product code and status.</summary>
public record TaskProductSummaryDto(
    string? ProductCode,
    string  Status,
    int     Count);

/// <summary>Result of GET /api/tasks/my/summary — cross-product task counts for the current user.</summary>
public record MyTaskSummaryResponse(
    IReadOnlyList<TaskProductSummaryDto> Summary,
    int                                  TotalOpen,
    int                                  TotalOverdue);

/// <summary>Linked entity DTO returned from linked-entity endpoints.</summary>
public record TaskLinkedEntityDto(
    Guid     Id,
    Guid     TaskId,
    string?  SourceProductCode,
    string   EntityType,
    string   EntityId,
    string   RelationshipType,
    DateTime CreatedAtUtc)
{
    public static TaskLinkedEntityDto From(TaskLinkedEntity e) => new(
        e.Id, e.TaskId, e.SourceProductCode, e.EntityType,
        e.EntityId, e.RelationshipType, e.CreatedAtUtc);
}

/// <summary>Request to add a linked entity to a task.</summary>
public record AddLinkedEntityRequest(
    string  EntityType,
    string  EntityId,
    string  RelationshipType  = "RELATED",
    string? SourceProductCode = null);
