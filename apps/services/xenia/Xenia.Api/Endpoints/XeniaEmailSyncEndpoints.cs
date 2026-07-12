using Microsoft.AspNetCore.Mvc;
using Xenia.Application.Email.Ingestion;
using Xenia.Application.TenantContext;

namespace Xenia.Api.Endpoints;

public static class XeniaEmailSyncEndpoints
{
    public static void MapXeniaEmailSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/email/sources/{sourceId}")
            .WithTags("Email Sync");

        // POST /email/sources/{sourceId}/sync — trigger manual sync
        group.MapPost("/sync", async (
            Guid sourceId,
            XeniaTenantContextAccessor tenantCtx,
            IEmailSyncService syncService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty)
                return Results.Unauthorized();

            var actorId = GetActorId(ctx);
            var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault();

            var result = await syncService.RequestSyncAsync(tc.TenantId, sourceId, actorId, correlationId, ct);

            if (result.SourceNotFound) return Results.NotFound(new { error = "Source not found." });
            if (result.SourceDisabled) return Results.UnprocessableEntity(new { error = result.SafeMessage });
            if (result.ModuleDisabled) return Results.UnprocessableEntity(new { error = result.SafeMessage });
            if (result.AlreadyRunning) return Results.Conflict(new { error = result.SafeMessage });
            if (!result.Accepted)      return Results.Problem(detail: result.SafeMessage, statusCode: 500);

            return Results.Accepted(null, new { runId = result.RunId, message = "Sync queued." });
        }).RequireAuthorization(XeniaPolicies.EmailSync);

        // GET /email/sources/{sourceId}/sync-state
        group.MapGet("/sync-state", async (
            Guid sourceId,
            XeniaTenantContextAccessor tenantCtx,
            ISyncStateService syncStateService,
            CancellationToken ct) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            var state = await syncStateService.GetSyncStateAsync(tc.TenantId, sourceId, ct);
            if (state is null) return Results.NotFound(new { error = "No sync state found for this source." });

            return Results.Ok(new
            {
                id                           = state.Id,
                emailSourceId                = state.EmailSourceId,
                providerType                 = state.ProviderType.ToString(),
                cursorType                   = state.CursorType.ToString(),
                safeCursorSummary            = state.SafeCursorSummary,
                lastSuccessfulSyncAt         = state.LastSuccessfulSyncAt,
                lastAttemptedSyncAt          = state.LastAttemptedSyncAt,
                initialSyncCompleted         = state.InitialSyncCompleted,
                consecutiveFailureCount      = state.ConsecutiveFailureCount,
                nextEligibleSyncAt           = state.NextEligibleSyncAt,
                lastErrorCode                = state.LastErrorCode,
                safeLastErrorSummary         = state.SafeLastErrorSummary,
            });
        }).RequireAuthorization(XeniaPolicies.EmailSync);

        // GET /email/sources/{sourceId}/ingestion-history
        group.MapGet("/ingestion-history", async (
            Guid sourceId,
            XeniaTenantContextAccessor tenantCtx,
            ISyncStateService syncStateService,
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageOffset = 0,
            CancellationToken ct = default) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            var runs = await syncStateService.GetIngestionHistoryAsync(
                tc.TenantId, sourceId, Math.Min(pageSize, 100), pageOffset, ct);

            return Results.Ok(new
            {
                runs = runs.Select(r => MapRunSummary(r)),
                pageSize,
                pageOffset,
            });
        }).RequireAuthorization(XeniaPolicies.EmailSync);

        // GET /email/sources/{sourceId}/ingestion-history/{runId}
        group.MapGet("/ingestion-history/{runId}", async (
            Guid sourceId,
            Guid runId,
            XeniaTenantContextAccessor tenantCtx,
            ISyncStateService syncStateService,
            CancellationToken ct) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            var run = await syncStateService.GetRunAsync(tc.TenantId, runId, ct);
            if (run is null || run.EmailSourceId != sourceId) return Results.NotFound();

            return Results.Ok(MapRunDetail(run));
        }).RequireAuthorization(XeniaPolicies.EmailSync);

        // POST /email/sources/{sourceId}/reset-sync — requires EmailManage (higher privilege)
        group.MapPost("/reset-sync", async (
            Guid sourceId,
            XeniaTenantContextAccessor tenantCtx,
            IEmailSyncService syncService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            var actorId      = GetActorId(ctx);
            var correlationId= ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault();

            var result = await syncService.ResetSyncAsync(tc.TenantId, sourceId, actorId, correlationId, ct);

            if (result.SourceNotFound) return Results.NotFound(new { error = "Source not found." });
            if (!result.Success)       return Results.Problem(detail: result.SafeMessage, statusCode: 500);

            return Results.Ok(new { message = result.SafeMessage });
        }).RequireAuthorization(XeniaPolicies.EmailManage);
    }

    private static Guid? GetActorId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static object MapRunSummary(Domain.Email.EmailIngestionRun r) => new
    {
        id               = r.Id,
        emailSourceId    = r.EmailSourceId,
        triggerType      = r.TriggerType.ToString(),
        status           = r.Status.ToString(),
        startedAt        = r.StartedAt,
        completedAt      = r.CompletedAt,
        durationMs       = r.DurationMs,
        messagesImported = r.MessagesImported,
        messagesDuplicated = r.MessagesDuplicated,
        messagesFailed   = r.MessagesFailed,
        pagesProcessed   = r.PagesProcessed,
        cursorAfterSafeSummary = r.CursorAfterSafeSummary,
    };

    private static object MapRunDetail(Domain.Email.EmailIngestionRun r) => new
    {
        id                    = r.Id,
        emailSourceId         = r.EmailSourceId,
        triggerType           = r.TriggerType.ToString(),
        status                = r.Status.ToString(),
        startedAt             = r.StartedAt,
        completedAt           = r.CompletedAt,
        durationMs            = r.DurationMs,
        correlationId         = r.CorrelationId,
        messagesDiscovered    = r.MessagesDiscovered,
        messagesImported      = r.MessagesImported,
        messagesUpdated       = r.MessagesUpdated,
        messagesDuplicated    = r.MessagesDuplicated,
        messagesFailed        = r.MessagesFailed,
        attachmentsDiscovered = r.AttachmentsDiscovered,
        attachmentsDispatched = r.AttachmentsDispatched,
        attachmentsFailed     = r.AttachmentsFailed,
        pagesProcessed        = r.PagesProcessed,
        retryCount            = r.RetryCount,
        cursorBeforeSafeSummary = r.CursorBeforeSafeSummary,
        cursorAfterSafeSummary  = r.CursorAfterSafeSummary,
        errorCode             = r.ErrorCode,
        safeErrorSummary      = r.SafeErrorSummary,
    };
}
