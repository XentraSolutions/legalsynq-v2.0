using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record CreateTaskRequest(
    string    Title,
    string?   Description       = null,
    string?   Priority          = null,
    string?   Scope             = null,
    Guid?     AssignedUserId    = null,
    string?   SourceProductCode = null,
    string?   SourceEntityType  = null,
    Guid?     SourceEntityId    = null,
    DateTime? DueAt             = null);

public record UpdateTaskRequest(
    string    Title,
    string?   Description    = null,
    string?   Priority       = null,
    Guid?     AssignedUserId = null,
    DateTime? DueAt          = null);

public record AssignTaskRequest(Guid? AssignedUserId);

public record TransitionStatusRequest(string Status);

public record AddNoteRequest(string Note);

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
