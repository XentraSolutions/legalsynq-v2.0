using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

/// <summary>
/// TENANT-B11 — Admin-focused tenant management endpoints.
///
/// These endpoints surface an enriched Tenant aggregate response compatible
/// with the control-center <c>mapTenantSummary</c> / <c>mapTenantDetail</c>
/// mappers, allowing Control Center to read from Tenant service instead of
/// Identity for tenant-management screens.
///
/// Routes (all require PlatformAdmin):
///   GET    /api/v1/admin/tenants              — paged list
///   GET    /api/v1/admin/tenants/{id}         — full detail (branding + entitlements + compat)
///   PATCH  /api/v1/admin/tenants/{id}/status  — targeted status update
/// </summary>
public static class TenantAdminEndpoints
{
    public static void MapTenantAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/tenants")
                       .RequireAuthorization(Policies.AdminOnly);

        // ── GET /api/v1/admin/tenants ─────────────────────────────────────────

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
    }

    private record StatusUpdateRequest(string? Status);
}
