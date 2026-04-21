using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .WithTags("Tasks");

        group.MapGet("/",                       ListTasks);
        group.MapGet("/my",                     GetMyTasks);
        group.MapGet("/my/summary",             GetMyTaskSummary);
        group.MapGet("/{id:guid}",              GetTaskById);
        group.MapPost("/",                      CreateTask);
        group.MapPut("/{id:guid}",              UpdateTask);
        group.MapPost("/{id:guid}/status",      TransitionStatus);
        group.MapPost("/{id:guid}/assign",      AssignTask);
        group.MapGet("/by-workflow/{wfId:guid}", GetByWorkflow);
        group.MapGet("/by-source-entity",       GetBySourceEntity);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async System.Threading.Tasks.Task<IResult> ListTasks(
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        string?   search              = null,
        string?   status              = null,
        string?   priority            = null,
        string?   scope               = null,
        Guid?     assignedUserId      = null,
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
        int       page                = 1,
        int       pageSize            = 50,
        CancellationToken ct          = default)
    {
        var tenantId    = RequireTenantId(ctx);
        var currentUser = ctx.UserId;
        var result      = await taskService.SearchAsync(
            tenantId, search, status, priority, scope,
            assignedUserId, sourceProductCode, stageId,
            dueBefore, dueAfter, workflowInstanceId,
            sourceEntityType, sourceEntityId,
            linkedEntityType, linkedEntityId,
            assignmentScope, currentUser,
            page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetMyTasks(
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        string?   productCode = null,
        string?   status      = null,
        int       page        = 1,
        int       pageSize    = 50,
        CancellationToken ct  = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.GetMyTasksAsync(tenantId, userId, productCode, status, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetMyTaskSummary(
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.GetMyTaskSummaryAsync(tenantId, userId, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetTaskById(
        Guid                   id,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> CreateTask(
        CreateTaskRequest      request,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/tasks/{result.Id}", result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpdateTask(
        Guid                   id,
        UpdateTaskRequest      request,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> TransitionStatus(
        Guid                    id,
        TransitionStatusRequest request,
        ITaskService            taskService,
        ICurrentRequestContext  ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.TransitionStatusAsync(tenantId, id, userId, request.Status, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> AssignTask(
        Guid                   id,
        AssignTaskRequest      request,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.AssignAsync(tenantId, id, userId, request.AssignedUserId, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetByWorkflow(
        Guid                   wfId,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetByWorkflowInstanceAsync(tenantId, wfId, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetBySourceEntity(
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        string                 entityType,
        Guid                   entityId,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetBySourceEntityAsync(tenantId, entityType, entityId, ct);
        return Results.Ok(result);
    }
}
