using Microsoft.AspNetCore.Mvc;
using Xenia.Application.Email.Operations;
using Xenia.Application.TenantContext;
using Xenia.Domain.Email;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Email retention and operational settings endpoints.
///
/// Settings read: EmailOperationsRead.
/// Settings manage: EmailOperationsManage.
/// Retention run: EmailRetentionManage.
/// History read: EmailOperationsRead.
/// Tenant context from JWT via XeniaTenantContextAccessor.
/// </summary>
public static class XeniaEmailRetentionEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailRetentionEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/email/operations/settings", GetSettingsAsync)
            .WithTags("EmailOperationalSettings")
            .WithSummary("Get operational settings for the authenticated tenant.")
            .WithName("GetEmailOperationalSettings")
            .RequireAuthorization(XeniaPolicies.EmailOperationsRead);

        app.MapPut("/api/v1/email/operations/settings", UpdateSettingsAsync)
            .WithTags("EmailOperationalSettings")
            .WithSummary("Update operational settings for the authenticated tenant.")
            .WithName("UpdateEmailOperationalSettings")
            .RequireAuthorization(XeniaPolicies.EmailOperationsManage);

        app.MapPost("/api/v1/email/operations/retention/run", RunRetentionAsync)
            .WithTags("EmailRetention")
            .WithSummary("Execute or simulate a retention run for the authenticated tenant.")
            .WithName("RunEmailRetention")
            .RequireAuthorization(XeniaPolicies.EmailRetentionManage);

        app.MapGet("/api/v1/email/operations/retention/history", GetRetentionHistoryAsync)
            .WithTags("EmailRetention")
            .WithSummary("Get recent retention run history.")
            .WithName("GetEmailRetentionHistory")
            .RequireAuthorization(XeniaPolicies.EmailOperationsRead);

        return app;
    }

    private static async Task<IResult> GetSettingsAsync(
        XeniaTenantContextAccessor tenantCtx,
        IEmailOperationalSettingsService settingsService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var settings = await settingsService.GetOrCreateAsync(tc.TenantId, ct);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateSettingsAsync(
        [FromBody] UpdateOperationalSettingsRequest request,
        XeniaTenantContextAccessor tenantCtx,
        IEmailOperationalSettingsService settingsService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        try
        {
            var updatedBy = tc.ActorId?.ToString() ?? "system";
            var settings  = await settingsService.UpdateAsync(tc.TenantId, request, updatedBy, ct);
            return Results.Ok(settings);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Concurrency"))
        {
            return Results.Conflict(new { error = "concurrency_conflict", message = ex.Message });
        }
    }

    private static async Task<IResult> RunRetentionAsync(
        [FromBody] RunRetentionRequest request,
        XeniaTenantContextAccessor tenantCtx,
        IRetentionService retentionService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var mode = request.DryRun ? EmailRetentionMode.DryRun : EmailRetentionMode.Execute;
        var correlationId = request.CorrelationId
            ?? ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        var run = await retentionService.ExecuteAsync(
            tc.TenantId, mode, tc.ActorId, correlationId, ct);

        return Results.Ok(run);
    }

    private static async Task<IResult> GetRetentionHistoryAsync(
        [FromQuery] int limit,
        XeniaTenantContextAccessor tenantCtx,
        IRetentionService retentionService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        limit = Math.Clamp(limit == 0 ? 20 : limit, 1, 100);
        var history = await retentionService.GetHistoryAsync(tc.TenantId, limit, ct);
        return Results.Ok(new { items = history, count = history.Count });
    }

    private sealed record RunRetentionRequest(bool DryRun, string? CorrelationId);
}
