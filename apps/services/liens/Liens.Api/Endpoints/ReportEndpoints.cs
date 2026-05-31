using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Liens.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        // ── v2 routes ─────────────────────────────────────────────────────────
        var v2 = app.MapGroup("/api/liens/reports/diy")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        v2.MapGet("/saved", GetSavedReports)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapGet("/saved/{id:guid}", GetSavedReportById)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapPost("/save", SaveReport)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapDelete("/saved/{id:guid}", DeleteReport)
            .RequirePermission(LiensPermissions.CaseUpdate);

        v2.MapPost("/run", RunReport)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapPost("/export", ExportReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // ── Legacy routes ─────────────────────────────────────────────────────
        var legacy = app.MapGroup("/report")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        // GET /report/diy  — list saved reports
        legacy.MapGet("/diy", GetSavedReports)
            .RequirePermission(LiensPermissions.CaseRead);

        // POST /report/diy  — run / preview a report
        legacy.MapPost("/diy", RunReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // POST /report/diy/export  — export as CSV
        legacy.MapPost("/diy/export", ExportReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // POST /report/diy/save  — save config
        legacy.MapPost("/diy/save", SaveReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // DELETE /report/diy/{id}
        legacy.MapDelete("/diy/{id:guid}", DeleteReport)
            .RequirePermission(LiensPermissions.CaseUpdate);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSavedReports(
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.GetSavedReportsAsync(tenantId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSavedReportById(
        Guid id,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetByIdAsync(tenantId, id, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SaveReport(
        SaveDIYReportRequest request,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.SaveReportAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/reports/diy/saved/{result.Id}", result);
    }

    private static async Task<IResult> DeleteReport(
        Guid id,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        await svc.DeleteReportAsync(tenantId, id, userId, ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Deleted." });
    }

    private static async Task<IResult> RunReport(
        DIYReportRunRequest request,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.RunReportAsync(tenantId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ExportReport(
        DIYReportRunRequest request,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.RunReportAsync(tenantId,
            new DIYReportRunRequest { Config = request.Config, Page = request.Page, Limit = 10_000, SortBy = request.SortBy, SortDir = request.SortDir },
            ct);

        var csv = BuildCsv(result.Items);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var base64 = Convert.ToBase64String(bytes);
        return Results.Ok(new { data = base64 });
    }

    private static string BuildCsv(List<DIYReportRow> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("CaseId,CaseNumber,ClientName,Status,LienTotal");
        foreach (var r in rows)
            sb.AppendLine($"{r.CaseId},{CsvEscape(r.CaseNumber)},{CsvEscape(r.ClientName)},{CsvEscape(r.Status)},{r.LienTotal}");
        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
