using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienTaskNoteService : ILienTaskNoteService
{
    private readonly ILienTaskRepository     _taskRepo;
    private readonly ILienTaskNoteRepository _noteRepo;
    private readonly IAuditPublisher         _audit;
    private readonly ILogger<LienTaskNoteService> _logger;

    public LienTaskNoteService(
        ILienTaskRepository     taskRepo,
        ILienTaskNoteRepository noteRepo,
        IAuditPublisher         audit,
        ILogger<LienTaskNoteService> logger)
    {
        _taskRepo = taskRepo;
        _noteRepo = noteRepo;
        _audit    = audit;
        _logger   = logger;
    }

    public async Task<List<TaskNoteResponse>> GetNotesAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        await EnsureTaskExistsAsync(tenantId, taskId, ct);
        var notes = await _noteRepo.GetByTaskIdAsync(tenantId, taskId, ct);
        return notes.Select(MapToResponse).ToList();
    }

    public async Task<TaskNoteResponse> CreateNoteAsync(
        Guid tenantId, Guid taskId, Guid actorUserId,
        CreateTaskNoteRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content is required."] });
        if (request.Content.Length > 5000)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content must not exceed 5000 characters."] });

        var task = await EnsureTaskExistsAsync(tenantId, taskId, ct);

        var note = LienTaskNote.Create(
            taskId:          taskId,
            tenantId:        tenantId,
            content:         request.Content,
            createdByUserId: actorUserId,
            createdByName:   request.CreatedByName.Trim().Length > 0
                                 ? request.CreatedByName.Trim()
                                 : "Unknown");

        await _noteRepo.AddAsync(note, ct);

        _logger.LogInformation("Task note created: NoteId={NoteId} TaskId={TaskId}", note.Id, taskId);

        _audit.Publish(
            eventType:   "liens.task_note.created",
            action:      "create",
            description: $"Note added to task '{task.Title}'",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienTaskNote",
            entityId:    note.Id.ToString(),
            metadata:    $"taskId={taskId}");

        // Optional case-timeline hook: if the task is linked to a case, publish a case-scoped event
        if (task.CaseId.HasValue)
        {
            _audit.Publish(
                eventType:   "liens.case.task_note_added",
                action:      "update",
                description: $"Note added to task '{task.Title}'",
                tenantId:    tenantId,
                actorUserId: actorUserId,
                entityType:  "Case",
                entityId:    task.CaseId.Value.ToString(),
                metadata:    $"taskId={taskId} noteId={note.Id}");
        }

        return MapToResponse(note);
    }

    public async Task<TaskNoteResponse> UpdateNoteAsync(
        Guid tenantId, Guid taskId, Guid noteId, Guid actorUserId,
        UpdateTaskNoteRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content is required."] });
        if (request.Content.Length > 5000)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content must not exceed 5000 characters."] });

        await EnsureTaskExistsAsync(tenantId, taskId, ct);

        var note = await _noteRepo.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new NotFoundException($"Note '{noteId}' not found.");

        if (note.TaskId != taskId)
            throw new NotFoundException($"Note '{noteId}' does not belong to task '{taskId}'.");

        if (note.IsDeleted)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Note"] = ["Cannot edit a deleted note."] });

        if (note.CreatedByUserId != actorUserId)
            throw new UnauthorizedAccessException("You can only edit your own notes.");

        note.Edit(request.Content, actorUserId);
        await _noteRepo.UpdateAsync(note, ct);

        _logger.LogInformation("Task note updated: NoteId={NoteId} TaskId={TaskId}", noteId, taskId);

        _audit.Publish(
            eventType:   "liens.task_note.updated",
            action:      "update",
            description: $"Note updated on task",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienTaskNote",
            entityId:    noteId.ToString(),
            metadata:    $"taskId={taskId}");

        return MapToResponse(note);
    }

    public async Task DeleteNoteAsync(
        Guid tenantId, Guid taskId, Guid noteId, Guid actorUserId,
        CancellationToken ct = default)
    {
        await EnsureTaskExistsAsync(tenantId, taskId, ct);

        var note = await _noteRepo.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new NotFoundException($"Note '{noteId}' not found.");

        if (note.TaskId != taskId)
            throw new NotFoundException($"Note '{noteId}' does not belong to task '{taskId}'.");

        if (note.IsDeleted) return;

        if (note.CreatedByUserId != actorUserId)
            throw new UnauthorizedAccessException("You can only delete your own notes.");

        note.SoftDelete();
        await _noteRepo.UpdateAsync(note, ct);

        _logger.LogInformation("Task note deleted: NoteId={NoteId} TaskId={TaskId}", noteId, taskId);

        _audit.Publish(
            eventType:   "liens.task_note.deleted",
            action:      "delete",
            description: $"Note deleted from task",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienTaskNote",
            entityId:    noteId.ToString(),
            metadata:    $"taskId={taskId}");
    }

    private async Task<LienTask> EnsureTaskExistsAsync(Guid tenantId, Guid taskId, CancellationToken ct)
    {
        return await _taskRepo.GetByIdAsync(tenantId, taskId, ct)
            ?? throw new NotFoundException($"Task '{taskId}' not found for tenant '{tenantId}'.");
    }

    private static TaskNoteResponse MapToResponse(LienTaskNote n) => new()
    {
        Id              = n.Id,
        TaskId          = n.TaskId,
        Content         = n.Content,
        CreatedByUserId = n.CreatedByUserId,
        CreatedByName   = n.CreatedByName,
        IsEdited        = n.IsEdited,
        CreatedAtUtc    = n.CreatedAtUtc,
        UpdatedAtUtc    = n.UpdatedAtUtc,
    };
}
