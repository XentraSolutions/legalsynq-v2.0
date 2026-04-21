using BuildingBlocks.Exceptions;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using TaskStatus = Task.Domain.Enums.TaskStatus;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository        _tasks;
    private readonly ITaskNoteRepository    _notes;
    private readonly ITaskHistoryRepository _history;
    private readonly IUnitOfWork            _uow;
    private readonly ILogger<TaskService>   _logger;

    public TaskService(
        ITaskRepository        tasks,
        ITaskNoteRepository    notes,
        ITaskHistoryRepository history,
        IUnitOfWork            uow,
        ILogger<TaskService>   logger)
    {
        _tasks   = tasks;
        _notes   = notes;
        _history = history;
        _uow     = uow;
        _logger  = logger;
    }

    public async System.Threading.Tasks.Task<TaskDto> CreateAsync(
        Guid              tenantId,
        Guid              createdByUserId,
        CreateTaskRequest request,
        CancellationToken ct = default)
    {
        var task = PlatformTask.Create(
            tenantId,
            request.Title,
            createdByUserId,
            request.Description,
            request.Priority,
            request.Scope,
            request.AssignedUserId,
            request.SourceProductCode,
            request.SourceEntityType,
            request.SourceEntityId,
            request.DueAt);

        await _tasks.AddAsync(task, ct);

        var historyEntry = TaskHistory.Record(
            task.Id, tenantId, TaskActions.Created, createdByUserId,
            $"Task '{task.Title}' created with scope {task.Scope}");
        await _history.AddAsync(historyEntry, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task {TaskId} created by {UserId} in tenant {TenantId} (scope={Scope})",
            task.Id, createdByUserId, tenantId, task.Scope);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var task = await _tasks.GetByIdAsync(tenantId, id, ct);
        return task is null ? null : TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskListResponse> SearchAsync(
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
        var (items, total) = await _tasks.SearchAsync(
            tenantId, search, status, priority, scope,
            assignedUserId, sourceProductCode, page, pageSize, ct);
        return new TaskListResponse(items.Select(TaskDto.From).ToList(), total, page, pageSize);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetMyTasksAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var tasks = await _tasks.GetByAssignedUserAsync(tenantId, userId, ct);
        return tasks.Select(TaskDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<TaskDto> UpdateAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        UpdateTaskRequest request,
        CancellationToken ct = default)
    {
        var task = await RequireTaskAsync(tenantId, id, ct);
        task.Update(request.Title, updatedByUserId, request.Description,
                    request.Priority, request.AssignedUserId, request.DueAt);

        var historyEntry = TaskHistory.Record(
            task.Id, tenantId, TaskActions.Updated, updatedByUserId);
        await _history.AddAsync(historyEntry, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task {TaskId} updated by {UserId} in tenant {TenantId}",
            task.Id, updatedByUserId, tenantId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto> TransitionStatusAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        string            newStatus,
        CancellationToken ct = default)
    {
        var task = await RequireTaskAsync(tenantId, id, ct);
        var oldStatus = task.Status;
        task.TransitionStatus(newStatus, updatedByUserId);

        var action = newStatus == TaskStatus.Completed ? TaskActions.Completed
                   : newStatus == TaskStatus.Cancelled ? TaskActions.Cancelled
                   : TaskActions.StatusChanged;

        var historyEntry = TaskHistory.Record(
            task.Id, tenantId, action, updatedByUserId,
            $"{oldStatus} → {newStatus}");
        await _history.AddAsync(historyEntry, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task {TaskId} status changed from {Old} to {New} by {UserId}",
            task.Id, oldStatus, newStatus, updatedByUserId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto> AssignAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        Guid?             assignedUserId,
        CancellationToken ct = default)
    {
        var task = await RequireTaskAsync(tenantId, id, ct);
        task.Assign(assignedUserId, updatedByUserId);

        var details = assignedUserId.HasValue
            ? $"Assigned to {assignedUserId}"
            : "Unassigned";
        var historyEntry = TaskHistory.Record(task.Id, tenantId, TaskActions.Assigned, updatedByUserId, details);
        await _history.AddAsync(historyEntry, ct);

        await _uow.SaveChangesAsync(ct);
        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskNoteDto> AddNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              createdByUserId,
        string            note,
        CancellationToken ct = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, ct);

        var noteEntity = TaskNote.Create(taskId, tenantId, note, createdByUserId);
        await _notes.AddAsync(noteEntity, ct);

        var historyEntry = TaskHistory.Record(taskId, tenantId, TaskActions.NoteAdded, createdByUserId);
        await _history.AddAsync(historyEntry, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Note {NoteId} added to task {TaskId} by {UserId}",
            noteEntity.Id, taskId, createdByUserId);

        return TaskNoteDto.From(noteEntity);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskNoteDto>> GetNotesAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);
        var notes = await _notes.GetByTaskAsync(tenantId, taskId, ct);
        return notes.Select(TaskNoteDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskHistoryDto>> GetHistoryAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);
        var entries = await _history.GetByTaskAsync(tenantId, taskId, ct);
        return entries.Select(TaskHistoryDto.From).ToList();
    }

    private async System.Threading.Tasks.Task<PlatformTask> RequireTaskAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(tenantId, id, ct);
        if (task is null)
            throw new NotFoundException($"Task {id} not found.");
        return task;
    }
}
