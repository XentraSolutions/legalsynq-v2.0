using BuildingBlocks.Exceptions;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using TaskStatus = Task.Domain.Enums.TaskStatus;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository          _tasks;
    private readonly ITaskNoteRepository      _notes;
    private readonly ITaskHistoryRepository   _history;
    private readonly ITaskGovernanceService   _governance;
    private readonly ITaskReminderService     _reminders;
    private readonly ITaskNotificationClient  _notifications;
    private readonly IUnitOfWork              _uow;
    private readonly ILogger<TaskService>     _logger;

    public TaskService(
        ITaskRepository          tasks,
        ITaskNoteRepository      notes,
        ITaskHistoryRepository   history,
        ITaskGovernanceService   governance,
        ITaskReminderService     reminders,
        ITaskNotificationClient  notifications,
        IUnitOfWork              uow,
        ILogger<TaskService>     logger)
    {
        _tasks         = tasks;
        _notes         = notes;
        _history       = history;
        _governance    = governance;
        _reminders     = reminders;
        _notifications = notifications;
        _uow           = uow;
        _logger        = logger;
    }

    public async System.Threading.Tasks.Task<TaskDto> CreateAsync(
        Guid              tenantId,
        Guid              createdByUserId,
        CreateTaskRequest request,
        CancellationToken ct = default)
    {
        var governance = await _governance.ResolveAsync(tenantId, request.SourceProductCode, ct);

        if (governance.RequireAssignee && request.AssignedUserId is null)
            throw new InvalidOperationException("Governance requires an assignee.");
        if (governance.RequireDueDate && request.DueAt is null)
            throw new InvalidOperationException("Governance requires a due date.");

        var task = PlatformTask.Create(
            tenantId,
            request.Title,
            createdByUserId,
            request.Description,
            request.Priority ?? governance.DefaultPriority,
            request.Scope ?? governance.DefaultTaskScope,
            request.AssignedUserId,
            request.SourceProductCode,
            request.SourceEntityType,
            request.SourceEntityId,
            request.DueAt);

        await _tasks.AddAsync(task, ct);

        await _history.AddAsync(
            TaskHistory.Record(task.Id, tenantId, TaskActions.Created, createdByUserId,
                $"Task '{task.Title}' created with scope {task.Scope}"), ct);

        await _uow.SaveChangesAsync(ct);

        if (task.DueAt.HasValue)
            await _reminders.SyncRemindersAsync(tenantId, task.Id, task.DueAt, ct);

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
        var task       = await RequireTaskAsync(tenantId, id, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (governance.RequireDueDate && request.DueAt is null)
            throw new InvalidOperationException("Governance requires a due date.");

        var previousDueAt = task.DueAt;
        task.Update(request.Title, updatedByUserId, request.Description,
                    request.Priority, request.AssignedUserId, request.DueAt);

        await _history.AddAsync(
            TaskHistory.Record(task.Id, tenantId, TaskActions.Updated, updatedByUserId), ct);

        await _uow.SaveChangesAsync(ct);

        if (task.DueAt != previousDueAt)
            await _reminders.SyncRemindersAsync(tenantId, task.Id, task.DueAt, ct);

        _logger.LogInformation(
            "Task {TaskId} updated by {UserId} in tenant {TenantId}", id, updatedByUserId, tenantId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto> TransitionStatusAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        string            newStatus,
        CancellationToken ct = default)
    {
        var task       = await RequireTaskAsync(tenantId, id, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (newStatus == TaskStatus.Cancelled && !governance.AllowCancel)
            throw new InvalidOperationException("Governance does not allow cancellation of tasks.");

        if (newStatus == TaskStatus.Completed
            && !governance.AllowCompleteWithoutStage
            && task.CurrentStageId is null)
            throw new InvalidOperationException("Governance requires a stage to be set before completing a task.");

        task.TransitionStatus(newStatus, updatedByUserId);

        var action = newStatus switch
        {
            TaskStatus.Completed => TaskActions.Completed,
            TaskStatus.Cancelled => TaskActions.Cancelled,
            _                    => TaskActions.StatusChanged,
        };

        await _history.AddAsync(
            TaskHistory.Record(task.Id, tenantId, action, updatedByUserId,
                $"Status changed to {newStatus}"), ct);

        await _uow.SaveChangesAsync(ct);

        if (TaskStatus.IsTerminal(newStatus))
            await _reminders.CancelRemindersAsync(tenantId, task.Id, ct);

        _logger.LogInformation(
            "Task {TaskId} status → {Status} by {UserId} in tenant {TenantId}",
            id, newStatus, updatedByUserId, tenantId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto> AssignAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        Guid?             assignedUserId,
        CancellationToken ct = default)
    {
        var task       = await RequireTaskAsync(tenantId, id, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (assignedUserId is null && !governance.AllowUnassign)
            throw new InvalidOperationException("Governance does not allow unassigning tasks.");

        var changeKind = task.Assign(assignedUserId, updatedByUserId);

        if (changeKind != AssignmentChangeKind.NoOp)
        {
            var (action, detail) = changeKind switch
            {
                AssignmentChangeKind.Assigned   => (TaskActions.Assigned,   $"Assigned to {assignedUserId}"),
                AssignmentChangeKind.Reassigned => (TaskActions.Reassigned, $"Reassigned to {assignedUserId}"),
                AssignmentChangeKind.Unassigned => (TaskActions.Unassigned, "Assignee removed"),
                _                               => (TaskActions.Updated,    string.Empty),
            };

            await _history.AddAsync(
                TaskHistory.Record(task.Id, tenantId, action, updatedByUserId, detail), ct);

            await _uow.SaveChangesAsync(ct);

            if (assignedUserId.HasValue)
            {
                try
                {
                    if (changeKind == AssignmentChangeKind.Assigned)
                        await _notifications.NotifyAssignedAsync(
                            tenantId, task.Id, task.Title,
                            assignedUserId.Value, task.SourceProductCode, ct);
                    else
                        await _notifications.NotifyReassignedAsync(
                            tenantId, task.Id, task.Title,
                            assignedUserId.Value, task.SourceProductCode, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Notification for task {TaskId} assignment could not be dispatched — continuing.",
                        task.Id);
                }
            }
        }

        _logger.LogInformation(
            "Task {TaskId} assignment change={Change} by {UserId} in tenant {TenantId}",
            id, changeKind, updatedByUserId, tenantId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskNoteDto> AddNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              createdByUserId,
        string            note,
        CancellationToken ct = default)
    {
        var task       = await RequireTaskAsync(tenantId, taskId, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (TaskStatus.IsTerminal(task.Status) && !governance.AllowNotesOnClosedTasks)
            throw new InvalidOperationException("Governance does not allow notes on closed tasks.");

        var noteEntity = TaskNote.Create(taskId, tenantId, note, createdByUserId);
        await _notes.AddAsync(noteEntity, ct);

        await _history.AddAsync(
            TaskHistory.Record(taskId, tenantId, TaskActions.NoteAdded, createdByUserId), ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Note {NoteId} added to task {TaskId} by {UserId}", noteEntity.Id, taskId, createdByUserId);

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
