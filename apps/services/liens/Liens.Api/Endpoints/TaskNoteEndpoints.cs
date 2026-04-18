using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-FLOW-004 — Task Notes endpoints.
/// Provides per-task notes (text-only collaboration layer).
/// </summary>
public static class TaskNoteEndpoints
{
    public static void MapTaskNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/tasks/{taskId:guid}/notes")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("TaskNotes");

        group.MapGet("/", GetNotes)
            .RequirePermission(LiensPermissions.TaskRead);

        group.MapPost("/", CreateNote)
            .RequirePermission(LiensPermissions.TaskNoteManage);

        group.MapPut("/{noteId:guid}", UpdateNote)
            .RequirePermission(LiensPermissions.TaskNoteManage);

        group.MapDelete("/{noteId:guid}", DeleteNote)
            .RequirePermission(LiensPermissions.TaskNoteManage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> GetNotes(
        Guid taskId,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var notes = await svc.GetNotesAsync(tenantId, taskId, ct);
        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateNote(
        Guid taskId,
        CreateTaskNoteRequest request,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.CreateNoteAsync(tenantId, taskId, userId, request, ct);
        return Results.Created($"/api/liens/tasks/{taskId}/notes/{note.Id}", note);
    }

    private static async Task<IResult> UpdateNote(
        Guid taskId,
        Guid noteId,
        UpdateTaskNoteRequest request,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.UpdateNoteAsync(tenantId, taskId, noteId, userId, request, ct);
        return Results.Ok(note);
    }

    private static async Task<IResult> DeleteNote(
        Guid taskId,
        Guid noteId,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await svc.DeleteNoteAsync(tenantId, taskId, noteId, userId, ct);
        return Results.NoContent();
    }
}
