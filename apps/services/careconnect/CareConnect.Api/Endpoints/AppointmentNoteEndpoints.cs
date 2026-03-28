using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AppointmentNoteEndpoints
{
    public static void MapAppointmentNoteEndpoints(this WebApplication app)
    {
        app.MapGet("/api/appointments/{appointmentId:guid}/notes", async (
            Guid appointmentId,
            IAppointmentNoteService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.GetByAppointmentAsync(tenantId, appointmentId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        app.MapPost("/api/appointments/{appointmentId:guid}/notes", async (
            Guid appointmentId,
            [FromBody] CreateAppointmentNoteRequest request,
            IAppointmentNoteService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.CreateAsync(tenantId, appointmentId, ctx.UserId, request, ct);
            return Results.Created($"/api/appointments/{appointmentId}/notes/{result.Id}", result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        app.MapPut("/api/appointment-notes/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAppointmentNoteRequest request,
            IAppointmentNoteService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
    }
}
