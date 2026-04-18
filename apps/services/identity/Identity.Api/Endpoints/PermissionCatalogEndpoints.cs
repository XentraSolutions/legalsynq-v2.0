using BuildingBlocks.Authorization;
using Identity.Application.Interfaces;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

/// <summary>
/// LS-ID-TNT-011 — Permission catalog inspection endpoints.
///
/// These routes expose the full seeded permission catalog and, optionally, the
/// effective permission set for a given user/tenant pair.
///
/// Auth: enforced at the gateway (all /api/admin/* routes require PlatformAdmin scope).
///       Tenant-scoped inspection (/api/permissions/effective) is called from the
///       Tenant Portal and accepts a standard JWT — gateway validates the token.
/// </summary>
public static class PermissionCatalogEndpoints
{
    public static IEndpointRouteBuilder MapPermissionCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        // ── Admin: full platform permission catalog ───────────────────────────
        // Returns all active permissions in the system, grouped by product code.
        // Used by the Control Center permission governance UI.
        routes.MapGet("/api/admin/permissions/catalog", GetPermissionCatalog);

        // ── Admin: role → permission assignments (system roles) ──────────────
        // Returns the current TenantAdmin / StandardUser role → permission seed mappings.
        routes.MapGet("/api/admin/permissions/role-assignments", GetRolePermissionAssignments);

        // ── Tenant: effective permissions for caller ──────────────────────────
        // Returns tenant + product permissions for the authenticated user.
        // Requires x-tenant-id header (set by gateway from JWT tenant claim).
        routes.MapGet("/api/permissions/effective", GetEffectivePermissions);

        return routes;
    }

    // ── Handler implementations ───────────────────────────────────────────────

    private static async Task<IResult> GetPermissionCatalog(IdentityDbContext db, CancellationToken ct)
    {
        var rows = await db.Permissions
            .Where(p => p.IsActive)
            .OrderBy(p => p.Product.Code)
            .ThenBy(p => p.Code)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.Category,
                ProductCode = p.Product.Code,
                ProductName = p.Product.Name,
                IsTenantLevel = p.Product.Code == ProductCodes.SynqPlatform,
            })
            .ToListAsync(ct);

        var grouped = rows
            .GroupBy(r => r.ProductCode)
            .Select(g => new
            {
                ProductCode = g.Key,
                ProductName = g.First().ProductName,
                IsTenantLevel = g.First().IsTenantLevel,
                Permissions = g.Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    p.Description,
                    p.Category,
                }).ToList(),
            })
            .OrderBy(g => g.IsTenantLevel ? 0 : 1)
            .ThenBy(g => g.ProductCode)
            .ToList();

        return Results.Ok(new
        {
            TotalPermissions = rows.Count,
            ProductCount = grouped.Count,
            Products = grouped,
        });
    }

    private static async Task<IResult> GetRolePermissionAssignments(IdentityDbContext db, CancellationToken ct)
    {
        var rows = await db.RolePermissionAssignments
            .Where(a => a.Permission.IsActive)
            .OrderBy(a => a.Role.Name)
            .ThenBy(a => a.Permission.Code)
            .Select(a => new
            {
                RoleId = a.RoleId,
                RoleName = a.Role.Name,
                PermissionId = a.PermissionId,
                PermissionCode = a.Permission.Code,
                ProductCode = a.Permission.Product.Code,
                AssignedAtUtc = a.AssignedAtUtc,
            })
            .ToListAsync(ct);

        var grouped = rows
            .GroupBy(r => r.RoleName)
            .Select(g => new
            {
                RoleName = g.Key,
                PermissionCount = g.Count(),
                Permissions = g.Select(p => new
                {
                    p.PermissionCode,
                    p.ProductCode,
                    p.AssignedAtUtc,
                }).ToList(),
            })
            .ToList();

        return Results.Ok(new
        {
            TotalMappings = rows.Count,
            RoleCount = grouped.Count,
            Roles = grouped,
        });
    }

    private static async Task<IResult> GetEffectivePermissions(
        IEffectivePermissionService permissionService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!Guid.TryParse(httpContext.Request.Headers["x-user-id"].FirstOrDefault(), out var userId))
            return Results.BadRequest(new { error = "x-user-id header is required and must be a valid GUID" });

        if (!Guid.TryParse(httpContext.Request.Headers["x-tenant-id"].FirstOrDefault(), out var tenantId))
            return Results.BadRequest(new { error = "x-tenant-id header is required and must be a valid GUID" });

        var result = await permissionService.GetEffectivePermissionsAsync(userId, tenantId, ct);

        return Results.Ok(new
        {
            UserId = userId,
            TenantId = tenantId,
            TenantPermissions = result.TenantPermissions,
            ProductPermissions = result.ProductPermissions,
            AllPermissions = result.AllPermissions,
            TotalCount = result.AllPermissions.Count,
        });
    }
}
