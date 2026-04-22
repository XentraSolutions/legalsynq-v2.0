using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

// CC2-INT-B06 — provider network management endpoints.
// Access: CARECONNECT_NETWORK_MANAGER product role, or PlatformAdmin / TenantAdmin bypass.
public static class NetworkEndpoints
{
    public static void MapNetworkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/networks")
            .RequireAuthorization(Policies.AuthenticatedUser);

        // ── List networks ──────────────────────────────────────────────────────
        group.MapGet("/", async (
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var networks = await service.GetAllAsync(tenantId, ct);
            return Results.Ok(networks);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Create network ─────────────────────────────────────────────────────
        group.MapPost("/", async (
            [FromBody] CreateNetworkRequest request,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var network = await service.CreateAsync(tenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/networks/{network.Id}", network);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Get network detail ─────────────────────────────────────────────────
        group.MapGet("/{id:guid}", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var network = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(network);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Update network ─────────────────────────────────────────────────────
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateNetworkRequest request,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var network = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(network);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Delete network ─────────────────────────────────────────────────────
        group.MapDelete("/{id:guid}", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await service.DeleteAsync(tenantId, id, ct);
            return Results.NoContent();
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Add provider to network ────────────────────────────────────────────
        group.MapPost("/{id:guid}/providers/{providerId:guid}", async (
            Guid id,
            Guid providerId,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await service.AddProviderAsync(tenantId, id, providerId, ctx.UserId, ct);
            return Results.NoContent();
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Remove provider from network ───────────────────────────────────────
        group.MapDelete("/{id:guid}/providers/{providerId:guid}", async (
            Guid id,
            Guid providerId,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await service.RemoveProviderAsync(tenantId, id, providerId, ct);
            return Results.NoContent();
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── Map markers for providers in network ───────────────────────────────
        group.MapGet("/{id:guid}/providers/markers", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var markers = await service.GetMarkersAsync(tenantId, id, ct);
            return Results.Ok(markers);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);

        // ── List providers in network ──────────────────────────────────────────
        group.MapGet("/{id:guid}/providers", async (
            Guid id,
            INetworkService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var detail = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(detail.Providers);
        })
        .RequireProductRole(ProductCodes.SynqCareConnect, ProductRoleCodes.CareConnectNetworkManager);
    }
}
