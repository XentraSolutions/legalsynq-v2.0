using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

public static class TaskNoteEndpoints
{
    public static void MapTaskNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks/{taskId:guid}")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .WithTags("Task Notes");

        group.MapPost("/notes",    AddNote);
        group.MapGet("/notes",     GetNotes);
        group.MapGet("/history",   GetHistory);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async System.Threading.Tasks.Task<IResult> AddNote(
        Guid                   taskId,
        AddNoteRequest         request,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.AddNoteAsync(tenantId, taskId, userId, request.Note, ct);
        return Results.Created($"/api/tasks/{taskId}/notes/{result.Id}", result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetNotes(
        Guid                   taskId,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetNotesAsync(tenantId, taskId, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetHistory(
        Guid                   taskId,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetHistoryAsync(tenantId, taskId, ct);
        return Results.Ok(result);
    }
}
