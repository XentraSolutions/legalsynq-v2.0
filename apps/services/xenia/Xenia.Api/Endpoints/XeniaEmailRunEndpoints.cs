using Microsoft.AspNetCore.Mvc;
using Xenia.Application.Email.Operations;
using Xenia.Application.TenantContext;
using Xenia.Domain.Email;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Email ingestion run endpoints — list, detail, retry, cancel.
///
/// List/detail: requires EmailOperationsRead.
/// Retry/cancel: requires EmailOperationsManage.
/// Tenant context from JWT via XeniaTenantContextAccessor.
/// </summary>
public static class XeniaEmailRunEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailRunEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/email/operations/runs", ListRunsAsync)
            .WithTags("EmailRuns")
            .WithSummary("List ingestion runs with filtering and pagination.")
            .WithName("ListEmailRuns")
            .RequireAuthorization(XeniaPolicies.EmailOperationsRead);

        app.MapGet("/api/v1/email/operations/runs/{runId:guid}", GetRunDetailAsync)
            .WithTags("EmailRuns")
            .WithSummary("Get detail for a single ingestion run.")
            .WithName("GetEmailRun")
            .RequireAuthorization(XeniaPolicies.EmailOperationsRead);

        app.MapPost("/api/v1/email/operations/runs/{runId:guid}/retry", RetryRunAsync)
            .WithTags("EmailRuns")
            .WithSummary("Queue a retry for a failed or completed-with-errors run.")
            .WithName("RetryEmailRun")
            .RequireAuthorization(XeniaPolicies.EmailOperationsManage);

        app.MapPost("/api/v1/email/operations/runs/{runId:guid}/cancel", CancelRunAsync)
            .WithTags("EmailRuns")
            .WithSummary("Request cancellation of an active or queued run.")
            .WithName("CancelEmailRun")
            .RequireAuthorization(XeniaPolicies.EmailOperationsManage);

        return app;
    }

    private static async Task<IResult> ListRunsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] Guid? sourceId,
        [FromQuery] string? status,
        [FromQuery] string? trigger,
        [FromQuery] bool? hasErrors,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? correlationId,
        XeniaTenantContextAccessor tenantCtx,
        IRunQueryService runService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        page     = Math.Max(1, page == 0 ? 1 : page);
        pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);

        IngestionRunStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<IngestionRunStatus>(status, ignoreCase: true, out var s))
            parsedStatus = s;

        IngestionRunTriggerType? parsedTrigger = null;
        if (!string.IsNullOrWhiteSpace(trigger) &&
            Enum.TryParse<IngestionRunTriggerType>(trigger, ignoreCase: true, out var t))
            parsedTrigger = t;

        var query = new RunListQuery(
            TenantId: tc.TenantId,
            From: from,
            To: to,
            SourceId: sourceId,
            Status: parsedStatus,
            Trigger: parsedTrigger,
            HasErrors: hasErrors,
            CorrelationId: correlationId,
            Page: page,
            PageSize: pageSize);

        var result = await runService.ListAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRunDetailAsync(
        Guid runId,
        XeniaTenantContextAccessor tenantCtx,
        IRunQueryService runService,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var detail = await runService.GetDetailAsync(tc.TenantId, runId, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> RetryRunAsync(
        Guid runId,
        [FromBody] RetryRunRequest? request,
        XeniaTenantContextAccessor tenantCtx,
        IRunQueryService runService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var correlationId = request?.CorrelationId
            ?? ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        var result = await runService.RetryAsync(tc.TenantId, runId, tc.ActorId, correlationId, ct);

        return result.Success
            ? Results.Ok(new { runId = result.NewRunId, message = "Retry queued successfully." })
            : Results.BadRequest(new { errorCode = result.ErrorCode, message = result.SafeMessage });
    }

    private static async Task<IResult> CancelRunAsync(
        Guid runId,
        [FromBody] CancelRunRequest? request,
        XeniaTenantContextAccessor tenantCtx,
        IRunQueryService runService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tc = tenantCtx.Current;
        if (tc is null || !tc.IsResolved) return Results.Unauthorized();

        var correlationId = request?.CorrelationId
            ?? ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        var result = await runService.CancelAsync(tc.TenantId, runId, tc.ActorId, correlationId, ct);

        return result.State switch
        {
            "NotFound"         => Results.NotFound(new { message = result.SafeMessage }),
            "AlreadyCompleted" => Results.Conflict(new { message = result.SafeMessage }),
            _                  => Results.Ok(new { state = result.State }),
        };
    }

    private sealed record RetryRunRequest(string? CorrelationId);
    private sealed record CancelRunRequest(string? CorrelationId);
}
