using Microsoft.AspNetCore.Mvc;
using Xenia.Application.Email.Operations;
using Xenia.Application.TenantContext;
using Xenia.Domain.Email;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Email Operational Alert endpoints.
///
/// List/detail: requires EmailOperationsRead.
/// Acknowledge/resolve/suppress: requires EmailAlertsManage.
/// Tenant context from JWT via XeniaTenantContextAccessor.
/// </summary>
public static class XeniaEmailAlertEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailAlertEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/email/operations/alerts", ListAlertsAsync)
            .WithTags("EmailAlerts")
            .WithSummary("List operational alerts for the authenticated tenant.")
            .WithName("ListEmailAlerts")
            .RequireAuthorization(XeniaPolicies.EmailOperationsRead);

        app.MapGet("/api/v1/email/operations/alerts/{alertId:guid}", GetAlertAsync)
            .WithTags("EmailAlerts")
            .WithSummary("Get a single alert.")
            .WithName("GetEmailAlert")
            .RequireAuthorization(XeniaPolicies.EmailOperationsRead);

        app.MapPost("/api/v1/email/operations/alerts/{alertId:guid}/acknowledge", AcknowledgeAlertAsync)
            .WithTags("EmailAlerts")
            .WithSummary("Acknowledge an open alert.")
            .WithName("AcknowledgeEmailAlert")
            .RequireAuthorization(XeniaPolicies.EmailAlertsManage);

        app.MapPost("/api/v1/email/operations/alerts/{alertId:guid}/resolve", ResolveAlertAsync)
            .WithTags("EmailAlerts")
            .WithSummary("Resolve an alert with optional reason.")
            .WithName("ResolveEmailAlert")
            .RequireAuthorization(XeniaPolicies.EmailAlertsManage);

        app.MapPost("/api/v1/email/operations/alerts/{alertId:guid}/suppress", SuppressAlertAsync)
            .WithTags("EmailAlerts")
            .WithSummary("Suppress an alert until a given UTC time.")
            .WithName("SuppressEmailAlert")
            .RequireAuthorization(XeniaPolicies.EmailAlertsManage);

        return app;
    }

    private static async Task<IResult> ListAlertsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] string? alertType,
        [FromQuery] Guid? sourceId,
        XeniaTenantContextAccessor tenantCtx,
        IAlertService alertService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        page     = Math.Max(1, page == 0 ? 1 : page);
        pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);

        EmailAlertStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<EmailAlertStatus>(status, ignoreCase: true, out var s))
            parsedStatus = s;

        EmailAlertSeverity? parsedSeverity = null;
        if (!string.IsNullOrWhiteSpace(severity) &&
            Enum.TryParse<EmailAlertSeverity>(severity, ignoreCase: true, out var sv))
            parsedSeverity = sv;

        EmailAlertType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(alertType) &&
            Enum.TryParse<EmailAlertType>(alertType, ignoreCase: true, out var at))
            parsedType = at;

        var query = new AlertListQuery(
            TenantId: tc.TenantId,
            Status: parsedStatus,
            Severity: parsedSeverity,
            AlertType: parsedType,
            EmailSourceId: sourceId,
            Page: page,
            PageSize: pageSize);

        var result = await alertService.ListAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAlertAsync(
        Guid alertId,
        XeniaTenantContextAccessor tenantCtx,
        IAlertService alertService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var alert = await alertService.GetAsync(tc.TenantId, alertId, ct);
        return alert is null ? Results.NotFound() : Results.Ok(alert);
    }

    private static async Task<IResult> AcknowledgeAlertAsync(
        Guid alertId,
        XeniaTenantContextAccessor tenantCtx,
        IAlertService alertService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();
        if (tc.ActorId is null) return Results.Unauthorized();

        var ok = await alertService.AcknowledgeAsync(tc.TenantId, alertId, tc.ActorId.Value, ct);
        return ok ? Results.Ok(new { acknowledged = true }) : Results.NotFound();
    }

    private static async Task<IResult> ResolveAlertAsync(
        Guid alertId,
        [FromBody] ResolveAlertRequest? request,
        XeniaTenantContextAccessor tenantCtx,
        IAlertService alertService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();
        if (tc.ActorId is null) return Results.Unauthorized();

        var ok = await alertService.ResolveAsync(tc.TenantId, alertId, tc.ActorId.Value, request?.Reason, ct);
        return ok ? Results.Ok(new { resolved = true }) : Results.NotFound();
    }

    private static async Task<IResult> SuppressAlertAsync(
        Guid alertId,
        [FromBody] SuppressAlertRequest request,
        XeniaTenantContextAccessor tenantCtx,
        IAlertService alertService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();
        if (tc.ActorId is null) return Results.Unauthorized();

        if (request.SuppressedUntil <= DateTime.UtcNow)
            return Results.BadRequest(new { error = "suppressed_until must be a future UTC time." });

        var ok = await alertService.SuppressAsync(
            tc.TenantId, alertId, tc.ActorId.Value, request.SuppressedUntil, ct);

        return ok ? Results.Ok(new { suppressed = true }) : Results.NotFound();
    }

    private sealed record ResolveAlertRequest(string? Reason);
    private sealed record SuppressAlertRequest(DateTime SuppressedUntil);
}
