using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/appointments", async (
            [FromBody] CreateAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var appointment = await service.CreateAppointmentAsync(tenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/appointments/{appointment.Id}", appointment);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        app.MapGet("/api/appointments", async (
            [AsParameters] AppointmentSearchParams query,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.SearchAppointmentsAsync(tenantId, query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        app.MapGet("/api/appointments/{id:guid}", async (
            Guid id,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var appointment = await service.GetAppointmentByIdAsync(tenantId, id, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);
    }
}
