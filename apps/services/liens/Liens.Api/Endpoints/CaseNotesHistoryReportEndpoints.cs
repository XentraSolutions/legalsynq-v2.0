using System.Globalization;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Api.Serialization;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class CaseNotesHistoryReportEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> SortFields =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["caseId"] = "caseId",
            ["caseName"] = "caseName",
            ["noteType"] = "noteType",
            ["noteDate"] = "noteDate",
            ["noteAuthor"] = "noteAuthor",
            ["noteContent"] = "noteContent",
        };

    public static void MapCaseNotesHistoryReportEndpoints(this WebApplication app)
    {
        var canonical = app.MapGroup("/api/liens/reports/case-notes-history")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequirePermission(LiensPermissions.CaseRead)
            .WithTags("CaseNotesHistory");

        canonical.MapPost("", GetCanonical);
        canonical.MapPost("/export", Export);

        var legacy = app.MapGroup("/report/case-notes-history")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequirePermission(LiensPermissions.CaseRead)
            .WithTags("CaseNotesHistory");

        legacy.MapPost("", GetLegacy);
        legacy.MapPost("/export", Export);
    }

    private static Task<IResult> GetCanonical(
        CaseNotesHistoryRequest? request,
        ICaseNotesHistoryReportService service,
        ICurrentRequestContext context,
        HttpContext httpContext,
        CancellationToken ct)
        => Get(request, service, context, httpContext, includeCreatedAtUtc: true, ct);

    private static Task<IResult> GetLegacy(
        CaseNotesHistoryRequest? request,
        ICaseNotesHistoryReportService service,
        ICurrentRequestContext context,
        HttpContext httpContext,
        CancellationToken ct)
        => Get(request, service, context, httpContext, includeCreatedAtUtc: false, ct);

    private static async Task<IResult> Get(
        CaseNotesHistoryRequest? request,
        ICaseNotesHistoryReportService service,
        ICurrentRequestContext context,
        HttpContext httpContext,
        bool includeCreatedAtUtc,
        CancellationToken ct)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        if (!TryNormalize(request, out var query, out var validationMessage))
            return ValidationError(validationMessage);

        var tenantId = context.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var page = await service.GetAsync(tenantId, query, ct);
        object data = includeCreatedAtUtc
            ? page.Items.Select(ToCanonicalRow).ToList()
            : page.Items.Select(ToLegacyRow).ToList();

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Case notes history retrieved.",
            data,
            page = page.Page,
            limit = page.Limit,
            totalCount = page.TotalCount,
            isComplete = page.ExcludedUnreconciledLegacyNoteCount == 0,
            excludedUnreconciledLegacyNoteCount = page.ExcludedUnreconciledLegacyNoteCount,
            warning = BuildCompletenessWarning(page.ExcludedUnreconciledLegacyNoteCount),
        });
    }

    private static async Task<IResult> Export(
        CaseNotesHistoryRequest? request,
        ICaseNotesHistoryReportService service,
        ICurrentRequestContext context,
        HttpContext httpContext,
        CancellationToken ct)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        if (!TryNormalize(request, out var query, out var validationMessage))
            return ValidationError(validationMessage);

        var tenantId = context.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var export = await service.ExportCsvAsync(tenantId, query, ct);
        if (export.SizeLimitExceeded)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "Export exceeds the 10 MiB file size limit. Narrow the selection and try again.",
                error = new { code = "validation_error" },
            });
        }

        var pacificNow = PacificTimeHelper.Convert(DateTime.UtcNow);
        var filename = $"case_notes_history_{query.NoteType.ToLowerInvariant()}_{pacificNow:yyyyMMddHHmmss}.csv";
        return Results.Ok(new
        {
            isSuccess = true,
            message = "CSV generated successfully.",
            isComplete = export.ExcludedUnreconciledLegacyNoteCount == 0,
            excludedUnreconciledLegacyNoteCount = export.ExcludedUnreconciledLegacyNoteCount,
            warning = BuildCompletenessWarning(export.ExcludedUnreconciledLegacyNoteCount),
            data = new object[]
            {
                new
                {
                    base64 = Convert.ToBase64String(export.Content),
                    filename,
                    export_format = "csv",
                },
            },
        });
    }

    private static bool TryNormalize(
        CaseNotesHistoryRequest? request,
        out CaseNotesHistoryQuery query,
        out string message)
    {
        query = null!;
        message = string.Empty;
        if (request is null)
        {
            message = "Request body is required.";
            return false;
        }

        var noteType = request.NoteType?.Trim().ToUpperInvariant();
        if (noteType is not ("TRACKING" or "FEED"))
        {
            message = "noteType must be TRACKING or FEED.";
            return false;
        }

        if (request.Page < 1)
        {
            message = "page must be at least 1.";
            return false;
        }

        if (request.Limit is < 1 or > 100)
        {
            message = "limit must be between 1 and 100.";
            return false;
        }

        var requestedSort = string.IsNullOrWhiteSpace(request.SortBy) ? "noteDate" : request.SortBy.Trim();
        if (!SortFields.TryGetValue(requestedSort, out var sortBy))
        {
            message = "sortBy must be one of: caseId, caseName, noteType, noteDate, noteAuthor, noteContent.";
            return false;
        }

        var direction = string.IsNullOrWhiteSpace(request.SortDirection)
            ? "desc"
            : request.SortDirection.Trim().ToLowerInvariant();
        if (direction is not ("asc" or "desc"))
        {
            message = "sortDirection must be ASC or DESC.";
            return false;
        }

        query = new CaseNotesHistoryQuery
        {
            NoteType = noteType,
            Page = request.Page,
            Limit = request.Limit,
            SortBy = sortBy,
            SortDirection = direction,
        };
        return true;
    }

    private static object ToCanonicalRow(CaseNotesHistoryRow row) => new
    {
        noteId = row.NoteId.ToString(),
        caseRecordId = row.CaseRecordId.ToString(),
        caseId = row.CaseId,
        caseName = row.CaseName,
        noteType = row.NoteType,
        noteTypeLabel = row.NoteTypeLabel,
        noteDate = FormatPacificDate(row.CreatedAtUtc),
        createdAtUtc = DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        noteAuthor = row.NoteAuthor,
        noteContent = row.NoteContent,
    };

    private static object ToLegacyRow(CaseNotesHistoryRow row) => new
    {
        noteId = row.NoteId.ToString(),
        caseRecordId = row.CaseRecordId.ToString(),
        caseId = row.CaseId,
        caseName = row.CaseName,
        noteType = row.NoteType,
        noteTypeLabel = row.NoteTypeLabel,
        noteDate = FormatPacificDate(row.CreatedAtUtc),
        noteAuthor = row.NoteAuthor,
        noteContent = row.NoteContent,
    };

    private static string FormatPacificDate(DateTime value)
        => PacificTimeHelper.Convert(value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IResult ValidationError(string message)
        => Results.BadRequest(new
        {
            isSuccess = false,
            message,
            error = new { code = "validation_error" },
        });

    private static object? BuildCompletenessWarning(int excludedCount)
        => excludedCount == 0
            ? null
            : new
        {
            code = "legacy_history_incomplete",
            message = "Some unreconciled legacy case notes were excluded. Native and reconciled notes are included.",
            excludedCount,
        };
}
