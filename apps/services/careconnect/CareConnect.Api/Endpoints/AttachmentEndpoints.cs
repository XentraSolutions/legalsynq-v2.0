using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
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
        .RequireAuthorization(Policies.AuthenticatedUser);

        app.MapPost("/api/referrals/{referralId:guid}/attachments", async (
            Guid referralId,
            [FromBody] CreateAttachmentMetadataRequest request,
            IReferralAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.CreateAsync(tenantId, referralId, ctx.UserId, request, ct);
            return Results.Created($"/api/referrals/{referralId}/attachments/{result.Id}", result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);

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
        .RequireAuthorization(Policies.AuthenticatedUser);

        app.MapPost("/api/appointments/{appointmentId:guid}/attachments", async (
            Guid appointmentId,
            [FromBody] CreateAttachmentMetadataRequest request,
            IAppointmentAttachmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.CreateAsync(tenantId, appointmentId, ctx.UserId, request, ct);
            return Results.Created($"/api/appointments/{appointmentId}/attachments/{result.Id}", result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
    }
}
