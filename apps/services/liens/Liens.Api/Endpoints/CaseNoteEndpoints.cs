using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain;
using System.Globalization;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-CASE-005 — Case Notes endpoints.
/// Provides per-case notes with category, pin/unpin, and audit trail.
/// </summary>
public static class CaseNoteEndpoints
{
    private sealed class LegacyCreateCaseNoteRequest
    {
        public string? caseId { get; init; }
        public string? note { get; init; }
    }

    private sealed class LegacyDeleteCaseNoteRequest
    {
        public string? noteId { get; init; }
    }

    private sealed class LegacyCaseNotesRequest
    {
        public string? caseId { get; init; }
        public string? showDeleted { get; init; } = "false";
        public string? sort { get; init; } = "newest";
    }

    private sealed class LegacyCaseNoteResponseItem
    {
        public string id { get; init; } = string.Empty;
        public string caseId { get; init; } = string.Empty;
        public string note { get; init; } = string.Empty;
        public string isDeleted { get; init; } = "N";
        public string created { get; init; } = string.Empty;
        public string createdBy { get; init; } = string.Empty;
        public string userId { get; init; } = string.Empty;
        public bool canDelete { get; init; }
    }

    public static void MapCaseNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/cases/{caseId:guid}/notes")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("CaseNotes");

        group.MapGet("/", GetNotes)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapPost("/", CreateNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapPut("/{noteId:guid}", UpdateNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapDelete("/{noteId:guid}", DeleteNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapPost("/{noteId:guid}/pin", PinNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapPost("/{noteId:guid}/unpin", UnpinNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        // Legacy compatibility routes from previous service /case endpoints.
        var legacyGroup = app.MapGroup("/api/liens/cases")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("CaseNotes");

        // Legacy: GET /case/notes/{caseId}
        legacyGroup.MapGet("/notes/{caseId:guid}", GetCaseNotesLegacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy: POST /case/get-notes
        legacyGroup.MapPost("/get-notes", GetCaseNotesFilteredLegacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // Legacy: POST /case/add-note
        legacyGroup.MapPost("/add-note", AddCaseNoteLegacy)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        // Legacy: POST /case/delete-note
        legacyGroup.MapPost("/delete-note", DeleteCaseNoteLegacy)
            .RequirePermission(LiensPermissions.CaseNoteManage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static string FormatLegacyTimestamp(DateTime value)
        => value.ToString("MM/dd/yyyy hh:mm tt", CultureInfo.InvariantCulture);

    private static async Task<IResult> GetCaseNotesLegacy(
        Guid caseId,
        ILienCaseNoteRepository repo,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);

        try
        {
            var notes = await repo.GetByCaseIdAsync(tenantId, caseId, ct);
            if (notes.Count == 0)
            {
                return Results.NotFound(new
                {
                    isSuccess = false,
                    message = "Error: No notes found",
                });
            }

            var data = notes
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new LegacyCaseNoteResponseItem
                {
                    id = n.Id.ToString(),
                    caseId = n.CaseId.ToString(),
                    note = n.Content,
                    isDeleted = n.IsDeleted ? "Y" : "N",
                    created = FormatLegacyTimestamp(n.CreatedAtUtc),
                    createdBy = n.CreatedByName,
                    userId = n.CreatedByUserId.ToString(),
                    canDelete = false,
                })
                .ToList();

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Notes list retrieved successfully.",
                data,
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No notes found",
            });
        }
    }

    private static async Task<IResult> GetCaseNotesFilteredLegacy(
        LegacyCaseNotesRequest request,
        ILienCaseNoteRepository repo,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.caseId))
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "caseId is required.",
            });
        }

        if (!Guid.TryParse(request.caseId, out var caseId))
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "caseId is required.",
            });
        }

        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        try
        {
            var notes = await repo.GetByCaseIdIncludingDeletedAsync(tenantId, caseId, ct);

            var showDeletedOnly = string.Equals(request.showDeleted, "true", StringComparison.OrdinalIgnoreCase);
            var sortOldest = string.Equals(request.sort, "oldest", StringComparison.OrdinalIgnoreCase);

            var query = notes.Where(n => showDeletedOnly ? n.IsDeleted : !n.IsDeleted);
            query = sortOldest
                ? query.OrderBy(n => n.CreatedAtUtc)
                : query.OrderByDescending(n => n.CreatedAtUtc);

            var data = query.Select(n => new LegacyCaseNoteResponseItem
            {
                id = n.Id.ToString(),
                caseId = n.CaseId.ToString(),
                note = n.Content,
                isDeleted = n.IsDeleted ? "Y" : "N",
                created = FormatLegacyTimestamp(n.CreatedAtUtc),
                createdBy = n.CreatedByName,
                userId = n.CreatedByUserId.ToString(),
                canDelete = n.CreatedByUserId == userId,
            }).ToList();

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Notes list retrieved successfully.",
                data,
            });
        }
        catch
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No notes found",
            });
        }
    }

    private static async Task<IResult> GetNotes(
        Guid caseId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var notes    = await svc.GetNotesAsync(tenantId, caseId, ct);
        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateNote(
        Guid caseId,
        CreateCaseNoteRequest request,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.CreateNoteAsync(tenantId, caseId, userId, request, ct);
        return Results.Created($"/api/liens/cases/{caseId}/notes/{note.Id}", note);
    }

    private static async Task<IResult> UpdateNote(
        Guid caseId,
        Guid noteId,
        UpdateCaseNoteRequest request,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.UpdateNoteAsync(tenantId, caseId, noteId, userId, request, ct);
        return Results.Ok(note);
    }

    private static async Task<IResult> DeleteNote(
        Guid caseId,
        Guid noteId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await svc.DeleteNoteAsync(tenantId, caseId, noteId, userId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PinNote(
        Guid caseId,
        Guid noteId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.PinNoteAsync(tenantId, caseId, noteId, userId, ct);
        return Results.Ok(note);
    }

    private static async Task<IResult> UnpinNote(
        Guid caseId,
        Guid noteId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.UnpinNoteAsync(tenantId, caseId, noteId, userId, ct);
        return Results.Ok(note);
    }

    private static async Task<IResult> AddCaseNoteLegacy(
        LegacyCreateCaseNoteRequest request,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.caseId, out var caseId) || string.IsNullOrWhiteSpace(request.note))
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "caseId and note are required.",
            });
        }

        await svc.CreateNoteAsync(
            tenantId,
            caseId,
            userId,
            new CreateCaseNoteRequest
            {
                Content = request.note.Trim(),
                Category = "general",
                CreatedByName = ctx.Email ?? ctx.Name ?? userId.ToString(),
            },
            ct);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Note added successfully.",
        });
    }

    private static async Task<IResult> DeleteCaseNoteLegacy(
        LegacyDeleteCaseNoteRequest request,
        ILienCaseNoteRepository repo,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!Guid.TryParse(request.noteId, out var noteId))
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "noteId is required.",
            });
        }

        var note = await repo.GetByIdAsync(tenantId, noteId, ct);
        if (note is null)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "Error: No notes found",
            });
        }

        await svc.DeleteNoteAsync(tenantId, note.CaseId, noteId, userId, ct);
        return Results.Ok(new
        {
            isSuccess = true,
            message = "Note deleted successfully.",
        });
    }
}
