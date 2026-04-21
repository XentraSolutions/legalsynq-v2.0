using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

/// <summary>
/// Flow integration endpoints.
///
/// POST /api/tasks/internal/flow-callback  — service-token protected; called by Flow or an orchestrator
///      when a workflow step transitions. Updates WorkflowStepKey on all linked tasks. Idempotent.
///
/// GET  /api/tasks/{id}/workflow-context   — returns the workflow linkage projection for a task.
/// PUT  /api/tasks/{id}/workflow-linkage   — admin-only; manually set workflow linkage on a task.
/// </summary>
public static class TaskFlowEndpoints
{
    public static void MapTaskFlowEndpoints(this WebApplication app)
    {
        // Internal callback — requires service token (PlatformAdmin role carries the service token claim)
        var internalGroup = app.MapGroup("/api/tasks/internal")
            .RequireAuthorization(Policies.AdminOnly)
            .WithTags("Tasks - Flow Integration");

        internalGroup.MapPost("/flow-callback", HandleFlowCallback);

        // Per-task workflow context — authenticated user
        var taskGroup = app.MapGroup("/api/tasks")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .WithTags("Tasks - Flow Integration");

        taskGroup.MapGet("/{id:guid}/workflow-context",  GetWorkflowContext);
        taskGroup.MapPut("/{id:guid}/workflow-linkage",  UpdateWorkflowLinkage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    /// <summary>
    /// Receives a workflow step-changed notification from the Flow service.
    /// Updates WorkflowStepKey on every PlatformTask that references the given WorkflowInstanceId.
    /// Returns a result summary including tasks updated and tasks skipped (already on that step).
    /// </summary>
    private static async System.Threading.Tasks.Task<IResult> HandleFlowCallback(
        FlowStepCallbackRequest request,
        ITaskService            taskService,
        CancellationToken       ct = default)
    {
        var result = await taskService.ProcessFlowCallbackAsync(request, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetWorkflowContext(
        Guid                   id,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetWorkflowContextAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpdateWorkflowLinkage(
        Guid                         id,
        UpdateWorkflowLinkageRequest request,
        ITaskService                 taskService,
        ICurrentRequestContext       ctx,
        CancellationToken            ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.UpdateWorkflowLinkageAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }
}
