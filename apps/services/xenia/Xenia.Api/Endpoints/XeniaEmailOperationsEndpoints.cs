using Microsoft.AspNetCore.Mvc;
using Xenia.Application.Email.Operations;
using Xenia.Application.TenantContext;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Email Operations endpoints — summary, source health, provider health.
///
/// All endpoints require EmailOperationsRead policy minimum.
/// Tenant context resolved from JWT via XeniaTenantContextAccessor.
/// </summary>
public static class XeniaEmailOperationsEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailOperationsEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/email/operations")
            .WithTags("EmailOperations")
            .RequireAuthorization(XeniaPolicies.EmailOperationsRead);

        grp.MapGet("/summary", GetSummaryAsync)
            .WithSummary("Get operations summary metrics for the authenticated tenant.")
            .WithName("GetEmailOperationsSummary");

        grp.MapGet("/sources/health", GetAllSourceHealthAsync)
            .WithSummary("Get health snapshots for all email sources.")
            .WithName("GetAllSourceHealth");

        grp.MapGet("/sources/{sourceId:guid}/health", GetSourceHealthAsync)
            .WithSummary("Get health snapshot for a single email source.")
            .WithName("GetSourceHealth");

        grp.MapGet("/providers/health", GetAllProviderHealthAsync)
            .WithSummary("Get health snapshots for all email providers.")
            .WithName("GetAllProviderHealth");

        grp.MapGet("/metrics", GetMetricsAsync)
            .WithSummary("Get time-window operational metrics for the authenticated tenant.")
            .WithName("GetEmailOperationsMetrics");

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? sourceId,
        XeniaTenantContextAccessor tenantCtx,
        IOperationsSummaryService summaryService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var query = new OperationsSummaryQuery(
            TenantId: tc.TenantId,
            From: from,
            To: to,
            SourceId: sourceId);

        var result = await summaryService.GetSummaryAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAllSourceHealthAsync(
        XeniaTenantContextAccessor tenantCtx,
        ISourceHealthService healthService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var snapshots = await healthService.GetAllAsync(tc.TenantId, ct);
        return Results.Ok(new { items = snapshots, count = snapshots.Count });
    }

    private static async Task<IResult> GetSourceHealthAsync(
        Guid sourceId,
        XeniaTenantContextAccessor tenantCtx,
        ISourceHealthService healthService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var snapshot = await healthService.GetAsync(tc.TenantId, sourceId, ct);
        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }

    private static async Task<IResult> GetAllProviderHealthAsync(
        XeniaTenantContextAccessor tenantCtx,
        IProviderHealthService providerHealthService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var snapshots = await providerHealthService.GetAllAsync(tc.TenantId, ct);
        return Results.Ok(new { items = snapshots, count = snapshots.Count });
    }

    private static async Task<IResult> GetMetricsAsync(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? sourceId,
        XeniaTenantContextAccessor tenantCtx,
        IOperationsSummaryService summaryService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var query = new OperationsSummaryQuery(
            TenantId: tc.TenantId,
            From: from,
            To: to,
            SourceId: sourceId);

        var summary = await summaryService.GetSummaryAsync(query, ct);

        var metrics = new
        {
            window = new { from = from ?? DateTimeOffset.UtcNow.AddHours(-24), to = to ?? DateTimeOffset.UtcNow },
            throughput = new
            {
                messagesImported       = summary.MessagesImported,
                duplicatesDropped      = summary.DuplicateMessages,
                messageFailures        = summary.MessageFailures,
                attachmentsDispatched  = summary.AttachmentsDispatched,
                attachmentFailures     = summary.AttachmentFailures,
            },
            runs = new
            {
                queued              = summary.QueuedRuns,
                active              = summary.ActiveRuns,
                completed           = summary.CompletedRuns,
                completedWithErrors = summary.CompletedWithErrorsRuns,
                failed              = summary.FailedRuns,
                retries             = summary.RetryCount,
                averageDurationMs   = summary.AverageSyncDurationMs,
            },
            health = new
            {
                healthySources      = summary.HealthySources,
                degradedSources     = summary.DegradedSources,
                unavailableSources  = summary.UnhealthySources,
                neverValidated      = summary.NeverValidatedSources,
            },
            incidents = new
            {
                providerThrottling  = summary.ProviderThrottlingIncidents,
                lockContention      = summary.LockContentionIncidents,
                cursorResets        = summary.CursorResets,
            },
            alerts = new
            {
                critical      = summary.OpenAlertsCritical,
                warning       = summary.OpenAlertsWarning,
                informational = summary.OpenAlertsInformational,
            },
            generatedAt = summary.GeneratedAt,
        };

        return Results.Ok(metrics);
    }
}
