using Microsoft.AspNetCore.Mvc;
using Xenia.Application.Email.Ingestion;
using Xenia.Application.TenantContext;
using Xenia.Domain.Email;

namespace Xenia.Api.Endpoints;

public static class XeniaEmailMessageEndpoints
{
    public static void MapXeniaEmailMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/email/messages")
            .WithTags("Email Messages");

        // GET /email/messages
        group.MapGet("/", async (
            XeniaTenantContextAccessor tenantCtx,
            IEmailMessageService messageService,
            [FromQuery] Guid? sourceId,
            [FromQuery] string? fromAddress,
            [FromQuery] string? subject,
            [FromQuery] string? importStatus,
            [FromQuery] bool? hasAttachments,
            [FromQuery] DateTime? receivedAfter,
            [FromQuery] DateTime? receivedBefore,
            [FromQuery] int pageSize = 50,
            [FromQuery] int pageOffset = 0,
            CancellationToken ct = default) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            MessageImportStatus? status = null;
            if (!string.IsNullOrWhiteSpace(importStatus) &&
                Enum.TryParse<MessageImportStatus>(importStatus, true, out var parsedStatus))
                status = parsedStatus;

            var query = new EmailMessageQuery
            {
                TenantId       = tc.TenantId,
                EmailSourceId  = sourceId,
                FromAddress    = fromAddress,
                SubjectContains= subject,
                ImportStatus   = status,
                HasAttachments = hasAttachments,
                ReceivedAfter  = receivedAfter,
                ReceivedBefore = receivedBefore,
                PageSize       = Math.Clamp(pageSize, 1, 200),
                PageOffset     = Math.Max(0, pageOffset),
            };

            var page = await messageService.ListMessagesAsync(query, ct);
            return Results.Ok(page);
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        // GET /email/messages/{id}
        group.MapGet("/{id}", async (
            Guid id,
            XeniaTenantContextAccessor tenantCtx,
            IEmailMessageService messageService,
            CancellationToken ct) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            var detail = await messageService.GetMessageAsync(tc.TenantId, id, ct);
            if (detail is null) return Results.NotFound();
            return Results.Ok(detail);
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        // GET /email/messages/{id}/attachments
        group.MapGet("/{id}/attachments", async (
            Guid id,
            XeniaTenantContextAccessor tenantCtx,
            IEmailMessageService messageService,
            CancellationToken ct) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            var attachments = await messageService.GetAttachmentsAsync(tc.TenantId, id, ct);
            return Results.Ok(new { attachments, total = attachments.Count });
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        // POST /email/messages/{id}/attachments/retry
        group.MapPost("/{id}/attachments/retry", async (
            Guid id,
            XeniaTenantContextAccessor tenantCtx,
            IEmailMessageService messageService,
            CancellationToken ct) =>
        {
            var tc = tenantCtx.Current;
            if (tc is null || tc.TenantId == Guid.Empty) return Results.Unauthorized();

            var result = await messageService.RetryAttachmentsAsync(tc.TenantId, id, tc.ActorId, ct);

            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    "NOT_FOUND"   => Results.NotFound(new { errorCode = result.ErrorCode, message = result.SafeMessage }),
                    "CONFLICT"    => Results.Conflict(new { errorCode = result.ErrorCode, message = result.SafeMessage }),
                    _             => Results.UnprocessableEntity(new { errorCode = result.ErrorCode, message = result.SafeMessage }),
                };
            }

            return Results.Ok(new { attachmentsQueued = result.AttachmentsQueued, message = "Attachment retry queued." });
        }).RequireAuthorization(XeniaPolicies.EmailManage);
    }
}
