using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Api.Serialization;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;

namespace Liens.Api.Endpoints;

public static class TaskEndpoints
{
    private sealed class LegacyUpdateTaskRequest
    {
        public string? taskId { get; init; }
        public string? title { get; init; }
        public string? dueDate { get; init; }
        public string? priority { get; init; }
        public string? status { get; init; }
        public string? assignedTo { get; init; }
        public string? description { get; init; }
    }

    private sealed class LegacyCreateTaskRequest
    {
        public string? caseId { get; init; }
        public string? title { get; init; }
        public string? dueDate { get; init; }
        public string? priority { get; init; }
        public string? status { get; init; }
        public string? assignedTo { get; init; }
        public string? description { get; init; }
    }

    private sealed class LegacyGetAndUpdateTaskRequest
    {
        public string? StatusId { get; init; }
    }

    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/tasks")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("Tasks");

        group.MapGet("/", ListTasks)
            .RequirePermission(LiensPermissions.TaskRead);

        group.MapGet("/{id:guid}", GetTaskById)
            .RequirePermission(LiensPermissions.TaskRead);

        group.MapPost("/", CreateTask)
            .RequirePermission(LiensPermissions.TaskCreate);

        group.MapPut("/{id:guid}", UpdateTask)
            .RequirePermission(LiensPermissions.TaskEditAll);

        group.MapPost("/{id:guid}/assign", AssignTask)
            .RequirePermission(LiensPermissions.TaskAssign);

        group.MapPost("/{id:guid}/status", UpdateStatus)
            .RequirePermission(LiensPermissions.TaskEditOwn);

        group.MapPost("/{id:guid}/complete", CompleteTask)
            .RequirePermission(LiensPermissions.TaskComplete);

        group.MapPost("/{id:guid}/cancel", CancelTask)
            .RequirePermission(LiensPermissions.TaskCancel);

        // Legacy compatibility route from previous service: POST /case/task/create
        // under the tasks base path becomes POST /api/liens/tasks/legacy/create.
        group.MapPost("/legacy/create", CreateTaskLegacy)
            .RequirePermission(LiensPermissions.TaskCreate);

        // Legacy compatibility route from previous service: GET /case/get-task/{caseId}/{taskId?}
        // under the tasks base path becomes GET /api/liens/tasks/legacy/get-task/{caseId}/{taskId?}.
        group.MapGet("/legacy/get-task/{caseId:guid}/{taskId?}", GetTasksLegacy)
            .RequirePermission(LiensPermissions.TaskRead);

        // Legacy compatibility route from previous service: PATCH /case/task/update
        // under the tasks base path becomes PATCH /api/liens/tasks/legacy/task/update.
        group.MapPatch("/legacy/task/update", UpdateTaskLegacy)
            .RequirePermission(LiensPermissions.TaskEditAll);

        // Legacy compatibility route from previous service: DELETE /case/task/delete/{taskId}
        // under the tasks base path becomes DELETE /api/liens/tasks/legacy/task/delete/{taskId}.
        group.MapDelete("/legacy/task/delete/{taskId}", DeleteTaskLegacy)
            .RequirePermission(LiensPermissions.TaskEditAll);

        var caseLegacy = app.MapGroup("/api/liens/cases")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("Tasks");

        caseLegacy.MapPost("/task/create", CreateTaskLegacy)
            .RequirePermission(LiensPermissions.TaskCreate);
        caseLegacy.MapPost("/tasks/create", CreateTaskLegacy)
            .RequirePermission(LiensPermissions.TaskCreate);
        caseLegacy.MapGet("/get-task/{caseId:guid}/{taskId?}", GetTasksLegacy)
            .RequirePermission(LiensPermissions.TaskRead);
        caseLegacy.MapPatch("/task/update", UpdateTaskLegacy)
            .RequirePermission(LiensPermissions.TaskEditAll);
        caseLegacy.MapPost("/task/update", UpdateTaskLegacy)
            .RequirePermission(LiensPermissions.TaskEditAll);
        caseLegacy.MapDelete("/task/delete/{taskId}", DeleteTaskLegacy)
            .RequirePermission(LiensPermissions.TaskEditAll);
        caseLegacy.MapPost("/task/{taskId}", GetAndUpdateTaskLegacy)
            .RequirePermission(LiensPermissions.TaskEditOwn);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> ListTasks(
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        string? priority = null,
        Guid? assignedUserId = null,
        Guid? caseId = null,
        Guid? lienId = null,
        Guid? workflowStageId = null,
        string? assignmentScope = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var tenantId      = RequireTenantId(ctx);
        var currentUserId = ctx.UserId;
        var result = await taskService.SearchAsync(
            tenantId, search, status, priority, assignedUserId, caseId, lienId,
            workflowStageId, assignmentScope, currentUserId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTaskById(
        Guid id,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Task '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateTask(
        CreateTaskRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/tasks/{result.Id}", result);
    }

    private static async Task<IResult> UpdateTask(
        Guid id,
        UpdateTaskRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AssignTask(
        Guid id,
        AssignTaskRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.AssignAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateStatus(
        Guid id,
        UpdateTaskStatusRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.UpdateStatusAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CompleteTask(
        Guid id,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.CompleteAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CancelTask(
        Guid id,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.CancelAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateTaskLegacy(
        LegacyCreateTaskRequest request,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId    = ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");
        var userId   = RequireUserId(ctx);

        if (!Guid.TryParse(request.caseId, out var caseId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error : Creating Case Task.",
            });
        }

        DateOnly? dueDate = null;
        if (!string.IsNullOrWhiteSpace(request.dueDate) &&
            DateOnly.TryParseExact(request.dueDate, ["yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy"], null, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            dueDate = parsedDate;
        }

        var noteFields = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.title))
            noteFields.Add($"title={request.title.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.status))
            noteFields.Add($"status={request.status.Trim()}");
        var priority = ResolveLegacyTaskPriority(request.priority);
        if (!string.IsNullOrWhiteSpace(request.priority) && priority is null)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = $"Invalid priority: '{request.priority.Trim()}'.",
            });
        }
        if (priority is not null)
            noteFields.Add($"priorityId={priority.Value.LegacyId}");

        try
        {
            var createRequest = new CreateServicingItemRequest
            {
                TaskNumber  = $"CT-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                TaskType    = "LegacyCaseTask",
                Description = request.description?.Trim() ?? request.title?.Trim() ?? string.Empty,
                AssignedTo  = request.assignedTo?.Trim() ?? string.Empty,
                Priority    = priority?.ServicingValue,
                CaseId      = caseId,
                DueDate     = dueDate,
                Notes       = noteFields.Count > 0 ? string.Join("; ", noteFields) : null,
            };

            await servicingItemService.CreateAsync(tenantId, orgId, userId, createRequest, ct);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Task created successfully.",
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = ex.Message,
            });
        }
    }

    private static async Task<IResult> GetTasksLegacy(
        Guid caseId,
        string? taskId,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            const int pageSize = 200;
            var page = 1;
            var rows = new List<ServicingItemResponse>();

            while (true)
            {
                var result = await servicingItemService.SearchAsync(
                    tenantId,
                    search: null,
                    status: null,
                    priority: null,
                    assignedTo: null,
                    caseId: caseId,
                    lienId: null,
                    page: page,
                    pageSize: pageSize,
                    ct);

                if (result.Items.Count == 0)
                    break;

                rows.AddRange(result.Items.Where(i =>
                    string.Equals(i.TaskType, "LegacyCaseTask", StringComparison.Ordinal)));

                if (rows.Count >= result.TotalCount)
                    break;

                page++;
            }

            if (!string.IsNullOrWhiteSpace(taskId))
            {
                rows = rows
                    .Where(i => string.Equals(i.Id.ToString(), taskId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var data = rows
                .OrderByDescending(i => i.CreatedAtUtc)
                .Select(i =>
                {
                    var noteFields = ParseLegacyNoteFields(i.Notes);
                    var status = noteFields.GetValueOrDefault("status", i.Status);
                    var priorityId = noteFields.GetValueOrDefault(
                        "priorityId",
                        ToLegacyTaskPriorityId(i.Priority));
                    return new
                    {
                        taskId = i.Id.ToString(),
                        caseId = i.CaseId?.ToString() ?? string.Empty,
                        title = noteFields.GetValueOrDefault("title", string.Empty),
                        description = i.Description,
                        dueDate = i.DueDate?.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                        priority = ToLegacyTaskPriorityName(priorityId),
                        priorityId,
                        status,
                        statusId = status,
                        assignedTo = i.AssignedTo,
                        createdAt = PacificTimeHelper.FormatTimestamp(i.CreatedAtUtc),
                    };
                })
                .ToList();

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Tasks list retrieved successfully.",
                data,
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No tasks found",
            });
        }
    }

    private static async Task<IResult> UpdateTaskLegacy(
        LegacyUpdateTaskRequest request,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        const string defaultErrorMessage = "Error : Updating Case Task.";

        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.taskId, out var taskId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = defaultErrorMessage,
            });
        }

        var existing = await servicingItemService.GetByIdAsync(tenantId, taskId, ct);
        if (existing is null || !string.Equals(existing.TaskType, "LegacyCaseTask", StringComparison.Ordinal))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = defaultErrorMessage,
            });
        }

        DateOnly? dueDate = existing.DueDate;
        if (!string.IsNullOrWhiteSpace(request.dueDate) &&
            DateOnly.TryParseExact(request.dueDate, ["yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy"], null, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            dueDate = parsedDate;
        }

        var noteFields = ParseLegacyNoteFields(existing.Notes);
        if (!string.IsNullOrWhiteSpace(request.title))
            noteFields["title"] = request.title.Trim();
        if (!string.IsNullOrWhiteSpace(request.status))
            noteFields["status"] = request.status.Trim();
        var servicingStatus = ResolveLegacyTaskServicingStatus(request.status, existing.Status);
        if (!string.IsNullOrWhiteSpace(request.status) && servicingStatus is null)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = $"Invalid status: '{request.status.Trim()}'.",
            });
        }
        var priority = ResolveLegacyTaskPriority(request.priority);
        if (!string.IsNullOrWhiteSpace(request.priority) && priority is null)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = $"Invalid priority: '{request.priority.Trim()}'.",
            });
        }
        if (priority is not null)
            noteFields["priorityId"] = priority.Value.LegacyId;

        try
        {
            var update = new UpdateServicingItemRequest
            {
                TaskType = existing.TaskType,
                Description = request.description?.Trim() ?? existing.Description,
                AssignedTo = request.assignedTo?.Trim() ?? existing.AssignedTo,
                AssignedToUserId = existing.AssignedToUserId,
                Priority = priority?.ServicingValue ?? existing.Priority,
                Status = servicingStatus ?? existing.Status,
                CaseId = existing.CaseId,
                LienId = existing.LienId,
                DueDate = dueDate,
                Notes = SerializeLegacyNoteFields(noteFields),
                Resolution = existing.Resolution,
            };

            await servicingItemService.UpdateAsync(tenantId, taskId, userId, update, ct);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully Updated Case Task.",
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = ex.Message,
            });
        }
    }

    private static async Task<IResult> DeleteTaskLegacy(
        string taskId,
        IServicingItemService servicingItemService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        const string defaultErrorMessage = "Error : Deleting Case Task.";

        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = defaultErrorMessage,
            });
        }

        var existing = await servicingItemService.GetByIdAsync(tenantId, parsedTaskId, ct);
        if (existing is null || !string.Equals(existing.TaskType, "LegacyCaseTask", StringComparison.Ordinal))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = defaultErrorMessage,
            });
        }

        try
        {
            await servicingItemService.DeleteAsync(tenantId, parsedTaskId, userId, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Successfully Deleted Case Task.",
            });
        }
        catch (Exception ex)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = ex.Message,
            });
        }
    }

    private static async Task<IResult> GetAndUpdateTaskLegacy(
        string taskId,
        LegacyGetAndUpdateTaskRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No tasks found",
            });
        }

        try
        {
            var task = await taskService.UpdateStatusAsync(
                tenantId,
                parsedTaskId,
                userId,
                new UpdateTaskStatusRequest
                {
                    Status = string.IsNullOrWhiteSpace(request.StatusId)
                        ? "Open"
                        : request.StatusId.Trim(),
                },
                ct);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Task updated successfully.",
                data = task,
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No tasks found",
            });
        }
    }

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq > 0)
            {
                var key = segment[..eq].Trim();
                var value = segment[(eq + 1)..].Trim();
                result[key] = value;
            }
        }

        return result;
    }

    private static string SerializeLegacyNoteFields(Dictionary<string, string> fields)
    {
        if (fields.Count == 0)
            return string.Empty;

        return string.Join("; ", fields.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static (string ServicingValue, string LegacyId)? ResolveLegacyTaskPriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant() switch
        {
            "LOW" => (ServicingPriority.Low, TaskPriorities.Low),
            "MEDIUM" or "NORMAL" => (ServicingPriority.Normal, TaskPriorities.Medium),
            "HIGH" => (ServicingPriority.High, TaskPriorities.High),
            "URGENT" => (ServicingPriority.Urgent, TaskPriorities.Urgent),
            _ => null,
        };
    }

    private static string? ResolveLegacyTaskServicingStatus(string? value, string existingStatus)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalized switch
        {
            "1" or "UPCOMING" or "NEW" or "OPEN" or "PENDING" => ServicingStatus.Pending,
            "2" or "INPROGRESS" => ServicingStatus.InProgress,
            "3" or "INREVIEW" or "WAITINGBLOCKED" or "ONHOLD" => ServicingStatus.OnHold,
            "4" or "COMPLETED" or "DONE" => ServicingStatus.Completed,
            // Legacy task status remains authoritative in Notes. ServicingItem has
            // no cancelled state, so preserve its current internal status.
            "CANCELLED" or "CANCELED" => existingStatus,
            _ => null,
        };
    }

    private static string ToLegacyTaskPriorityId(string priority) =>
        priority.Trim().ToUpperInvariant() switch
        {
            "LOW" => TaskPriorities.Low,
            "NORMAL" or "MEDIUM" => TaskPriorities.Medium,
            "HIGH" => TaskPriorities.High,
            "URGENT" => TaskPriorities.Urgent,
            _ => priority,
        };

    private static string ToLegacyTaskPriorityName(string priorityId) =>
        priorityId.Trim().ToUpperInvariant() switch
        {
            TaskPriorities.Low => "Low",
            TaskPriorities.Medium => "Medium",
            TaskPriorities.High => "High",
            TaskPriorities.Urgent => "Urgent",
            _ => priorityId,
        };
}
