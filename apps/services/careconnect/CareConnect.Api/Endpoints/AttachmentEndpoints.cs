using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        // ── Referral attachments ──────────────────────────────────────────────

        app.MapGet("/api/referrals/{referralId:guid}/attachments", async (
            Guid referralId,
            IReferralAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.GetByReferralAsync(tenantId, referralId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // CC2-INT-B03: Legacy metadata-only attachment creation is disabled.
        // All attachment creation must go through the server-side upload proxy (/attachments/upload)
        // which proxies file bytes to the Documents service before persisting the documentId.
        // Clients still on this endpoint should migrate to POST /attachments/upload.
        app.MapPost("/api/referrals/{referralId:guid}/attachments", (Guid referralId) =>
            Results.Problem(
                title:      "Endpoint removed",
                detail:     "Direct metadata attachment creation is no longer supported. " +
                            "Use POST /api/referrals/{id}/attachments/upload to upload a file.",
                statusCode: 410))
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // CC2-INT-B03: Server-side upload proxying — multipart/form-data file upload.
        // CareConnect forwards bytes to Documents service; only the documentId is persisted locally.
        app.MapPost("/api/referrals/{referralId:guid}/attachments/upload", async (
            Guid referralId,
            HttpRequest httpRequest,
            IReferralAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            if (!httpRequest.HasFormContentType)
                return Results.BadRequest(new { error = "Request must be multipart/form-data." });

            var form = await httpRequest.ReadFormAsync(ct);
            if (form.Files.Count == 0)
                return Results.BadRequest(new { error = "No file was provided." });

            var file         = form.Files[0];
            var scope        = form["scope"].FirstOrDefault() ?? AttachmentScope.Shared;
            var notes        = form["notes"].FirstOrDefault();
            var uploadRequest = new UploadAttachmentRequest { Scope = scope, Notes = notes };

            await using var stream = file.OpenReadStream();
            var result = await service.UploadAsync(
                tenantId,
                referralId,
                ctx.UserId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                uploadRequest,
                ct);

            return Results.Created($"/api/referrals/{referralId}/attachments/{result.Id}", result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .DisableAntiforgery();

        // CC2-INT-B03: Signed URL endpoint — returns a short-lived URL for document access.
        // Scope rules are enforced before the Documents service is called.
        app.MapGet("/api/referrals/{referralId:guid}/attachments/{attachmentId:guid}/url", async (
            Guid referralId,
            Guid attachmentId,
            [FromQuery] bool download,
            IReferralAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var isAdmin  = ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase);

            try
            {
                var result = await service.GetSignedUrlAsync(
                    tenantId,
                    referralId,
                    attachmentId,
                    callerOrgId:   ctx.OrgId,
                    callerOrgType: ctx.OrgType,
                    isAdmin:       isAdmin,
                    isDownload:    download,
                    ct:            ct);

                if (result is null)
                    // Note: 503 covers both "document not found" and transient Documents service failures.
                    // Future improvement: expose upstream status in DocumentSignedUrlResult to allow
                    // returning 404 (not found) vs 502 (upstream error) vs 503 (service unavailable).
                    return Results.Problem("The document is not currently accessible.", statusCode: 503);

                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Forbid();
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // ── Appointment attachments ───────────────────────────────────────────

        app.MapGet("/api/appointments/{appointmentId:guid}/attachments", async (
            Guid appointmentId,
            IAppointmentAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.GetByAppointmentAsync(tenantId, appointmentId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // CC2-INT-B03: Legacy metadata-only attachment creation is disabled — see referral endpoint note.
        app.MapPost("/api/appointments/{appointmentId:guid}/attachments", (Guid appointmentId) =>
            Results.Problem(
                title:      "Endpoint removed",
                detail:     "Direct metadata attachment creation is no longer supported. " +
                            "Use POST /api/appointments/{id}/attachments/upload to upload a file.",
                statusCode: 410))
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        // CC2-INT-B03: Server-side upload proxying for appointment attachments.
        app.MapPost("/api/appointments/{appointmentId:guid}/attachments/upload", async (
            Guid appointmentId,
            HttpRequest httpRequest,
            IAppointmentAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            if (!httpRequest.HasFormContentType)
                return Results.BadRequest(new { error = "Request must be multipart/form-data." });

            var form = await httpRequest.ReadFormAsync(ct);
            if (form.Files.Count == 0)
                return Results.BadRequest(new { error = "No file was provided." });

            var file          = form.Files[0];
            var notes         = form["notes"].FirstOrDefault();
            var scope         = form["scope"].FirstOrDefault() ?? AttachmentScope.Shared;
            var uploadRequest = new UploadAttachmentRequest { Scope = scope, Notes = notes };

            await using var stream = file.OpenReadStream();
            var result = await service.UploadAsync(
                tenantId,
                appointmentId,
                ctx.UserId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                uploadRequest,
                ct);

            return Results.Created($"/api/appointments/{appointmentId}/attachments/{result.Id}", result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect)
        .DisableAntiforgery();

        // CC2-INT-B03: Signed URL endpoint for appointment documents.
        app.MapGet("/api/appointments/{appointmentId:guid}/attachments/{attachmentId:guid}/url", async (
            Guid appointmentId,
            Guid attachmentId,
            [FromQuery] bool download,
            IAppointmentAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var isAdmin  = ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase);

            try
            {
                var result = await service.GetSignedUrlAsync(
                    tenantId,
                    appointmentId,
                    attachmentId,
                    callerOrgId:   ctx.OrgId,
                    callerOrgType: ctx.OrgType,
                    isAdmin:       isAdmin,
                    isDownload:    download,
                    ct:            ct);

                if (result is null)
                    // Note: 503 covers both "document not found" and transient Documents service failures.
                    // Future improvement: expose upstream status in DocumentSignedUrlResult to allow
                    // returning 404 (not found) vs 502 (upstream error) vs 503 (service unavailable).
                    return Results.Problem("The document is not currently accessible.", statusCode: 503);

                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
