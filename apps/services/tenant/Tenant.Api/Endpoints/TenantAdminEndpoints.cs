using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

/// <summary>
/// TENANT-B11/B12 — Admin-focused tenant management endpoints.
///
/// B11: GET list, GET detail, PATCH status.
/// B12: POST create (canonical Tenant-first creation),
///      POST entitlement toggle (Tenant-first, best-effort Identity sync).
///
/// Routes (all require AdminOnly):
///   GET    /api/v1/admin/tenants                                     — paged list
///   GET    /api/v1/admin/tenants/{id}                                — full detail
///   PATCH  /api/v1/admin/tenants/{id}/status                         — status update
///   POST   /api/v1/admin/tenants                                     — CANONICAL CREATE (B12)
///   POST   /api/v1/admin/tenants/{id}/entitlements/{productCode}     — entitlement toggle (B12)
/// </summary>
public static class TenantAdminEndpoints
{
    public static void MapTenantAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/tenants")
                       .RequireAuthorization(Policies.AdminOnly);

        // ── POST /api/v1/admin/tenants ─────────────────────────────────────────
        // TENANT-B12 — Canonical Tenant-first creation entry point.
        // Creates Tenant DB record first, then calls Identity for admin-user/org/provisioning.

        group.MapPost("/", async (
            AdminCreateTenantRequest body,
            ITenantAdminService      svc,
            CancellationToken        ct) =>
        {
            var result = await svc.CreateTenantAsync(body, ct);
            return Results.Created($"/api/v1/admin/tenants/{result.TenantId}", result);
        });

        // ── GET /api/v1/admin/tenants ──────────────────────────────────────────

        group.MapGet("/", async (
            ITenantAdminService svc,
            CancellationToken   ct,
            int  page     = 1,
            int  pageSize = 20) =>
        {
            var (items, total) = await svc.ListAdminAsync(page, pageSize, ct);
            return Results.Ok(new
            {
                items,
                totalCount = total,
                page,
                pageSize,
            });
        });

        // ── GET /api/v1/admin/tenants/{id} ────────────────────────────────────

        group.MapGet("/{id:guid}", async (
            Guid                id,
            ITenantAdminService svc,
            CancellationToken   ct) =>
        {
            var result = await svc.GetAdminDetailAsync(id, ct);
            if (result is null) throw new NotFoundException($"Tenant '{id}' was not found.");
            return Results.Ok(result);
        });

        // ── PATCH /api/v1/admin/tenants/{id}/status ───────────────────────────

        group.MapPatch("/{id:guid}/status", async (
            Guid                id,
            StatusUpdateRequest body,
            ITenantAdminService svc,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Status))
                return Results.BadRequest(new { error = "status is required." });

            var result = await svc.UpdateStatusAsync(id, body.Status, ct);
            return Results.Ok(result);
        });

        // ── POST /api/v1/admin/tenants/{id}/entitlements/{productCode} ─────────
        // TENANT-B12 — Admin entitlement toggle (Tenant-first, best-effort Identity sync).
        // Response shape is compatible with control-center mapEntitlementResponse mapper.

        group.MapPost("/{id:guid}/entitlements/{productCode}", async (
            Guid                id,
            string              productCode,
            EntitlementToggleRequest body,
            ITenantAdminService svc,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return Results.BadRequest(new { error = "productCode is required." });

            var result = await svc.ToggleEntitlementAsync(id, productCode, body.Enabled, ct);
            return Results.Ok(result);
        });
    }

    private record StatusUpdateRequest(string? Status);
    private record EntitlementToggleRequest(bool Enabled);
}
