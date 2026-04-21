using Task.Application.DTOs;

namespace Task.Application.Interfaces;

public interface ITaskService
{
    System.Threading.Tasks.Task<TaskDto> CreateAsync(
        Guid              tenantId,
        Guid              createdByUserId,
        CreateTaskRequest request,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto?> GetByIdAsync(
        Guid              tenantId,
        Guid              id,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskListResponse> SearchAsync(
        Guid              tenantId,
        string?           search            = null,
        string?           status            = null,
        string?           priority          = null,
        string?           scope             = null,
        Guid?             assignedUserId    = null,
        string?           sourceProductCode = null,
        int               page              = 1,
        int               pageSize          = 50,
        CancellationToken ct                = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetMyTasksAsync(
        Guid              tenantId,
        Guid              userId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto> UpdateAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        UpdateTaskRequest request,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto> TransitionStatusAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        string            newStatus,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto> AssignAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        Guid?             assignedUserId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskNoteDto> AddNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              createdByUserId,
        string            note,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskNoteDto>> GetNotesAsync(
        Guid              tenantId,
        Guid              taskId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskHistoryDto>> GetHistoryAsync(
        Guid              tenantId,
        Guid              taskId,
        CancellationToken ct = default);
}
