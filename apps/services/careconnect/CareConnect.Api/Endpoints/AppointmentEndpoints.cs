using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Authorization;
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
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, CapabilityCodes.AppointmentCreate, ct);
            var appointment = await service.CreateAppointmentAsync(tenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/appointments/{appointment.Id}", appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

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

        app.MapPut("/api/appointments/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, CapabilityCodes.AppointmentUpdate, ct);
            var appointment = await service.UpdateAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        app.MapPost("/api/appointments/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, CapabilityCodes.AppointmentManage, ct);
            var appointment = await service.CancelAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        app.MapPost("/api/appointments/{id:guid}/reschedule", async (
            Guid id,
            [FromBody] RescheduleAppointmentRequest request,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, CapabilityCodes.AppointmentManage, ct);
            var appointment = await service.RescheduleAppointmentAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(appointment);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        app.MapGet("/api/appointments/{id:guid}/history", async (
            Guid id,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var history = await service.GetAppointmentHistoryAsync(tenantId, id, ct);
            return Results.Ok(history);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);
    }
}
