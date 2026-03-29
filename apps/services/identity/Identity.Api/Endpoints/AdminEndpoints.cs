using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

/// <summary>
/// Admin endpoints consumed exclusively by the LegalSynq Control Center.
/// All routes are prefixed /api/admin/... and are accessed via the YARP
/// gateway under /identity/api/admin/... 
///
/// Auth is enforced at the gateway layer (JWT cookie validation) — the
/// Identity service trusts all forwarded requests unconditionally.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        // ── Tenants ──────────────────────────────────────────────────────────
        routes.MapGet("/api/admin/tenants",         ListTenants);
        routes.MapGet("/api/admin/tenants/{id:guid}", GetTenant);
        routes.MapPost("/api/admin/tenants/{id:guid}/entitlement", UpdateEntitlement);

        // ── Users ─────────────────────────────────────────────────────────
        routes.MapGet("/api/admin/users",           ListUsers);
        routes.MapGet("/api/admin/users/{id:guid}", GetUser);

        // ── Roles ──────────────────────────────────────────────────────────
        routes.MapGet("/api/admin/roles",           ListRoles);
        routes.MapGet("/api/admin/roles/{id:guid}", GetRole);

        // ── Audit Logs ────────────────────────────────────────────────────
        routes.MapGet("/api/admin/audit",           ListAudit);

        // ── Platform Settings (static seed — no DB table yet) ─────────────
        routes.MapGet("/api/admin/settings",            ListSettings);
        routes.MapPut("/api/admin/settings/{key}",      UpdateSetting);

        // ── Support Cases (not yet persisted — empty stubs) ───────────────
        routes.MapGet("/api/admin/support",             ListSupport);
        routes.MapGet("/api/admin/support/{id}",        GetSupport);
        routes.MapPost("/api/admin/support",            CreateSupport);
        routes.MapPost("/api/admin/support/{id}/notes", AddSupportNote);
        routes.MapPatch("/api/admin/support/{id}/status", UpdateSupportStatus);

        return routes;
    }

    // =========================================================================
    // TENANTS
    // =========================================================================

    private static async Task<IResult> ListTenants(
        IdentityDbContext db,
        int    page     = 1,
        int    pageSize = 20,
        string search   = "")
    {
        var q = db.Tenants
            .Include(t => t.Users)
            .Include(t => t.Organizations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(t => t.Name.Contains(search) || t.Code.Contains(search));

        var total = await q.CountAsync();

        var tenants = await q
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                id                 = t.Id,
                code               = t.Code,
                displayName        = t.Name,
                type               = "LawFirm",
                status             = t.IsActive ? "Active" : "Inactive",
                primaryContactName = t.Users.OrderBy(u => u.CreatedAtUtc).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? "",
                isActive           = t.IsActive,
                userCount          = t.Users.Count,
                orgCount           = t.Organizations.Count,
                createdAtUtc       = t.CreatedAtUtc,
            })
            .ToListAsync();

        return Results.Ok(new
        {
            items      = tenants,
            totalCount = total,
            page,
            pageSize,
        });
    }

    private static async Task<IResult> GetTenant(Guid id, IdentityDbContext db)
    {
        var t = await db.Tenants
            .Include(t => t.Users)
            .Include(t => t.Organizations)
            .Include(t => t.TenantProducts)
                .ThenInclude(tp => tp.Product)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (t is null) return Results.NotFound();

        var firstUser = t.Users.OrderBy(u => u.CreatedAtUtc).FirstOrDefault();

        var entitlements = t.TenantProducts.Select(tp => new
        {
            productCode  = tp.Product.Code,
            productName  = tp.Product.Name,
            enabled      = tp.IsEnabled,
            status       = tp.IsEnabled ? "Active" : "Disabled",
            enabledAtUtc = tp.EnabledAtUtc,
        }).ToList();

        return Results.Ok(new
        {
            id                 = t.Id,
            code               = t.Code,
            displayName        = t.Name,
            type               = "LawFirm",
            status             = t.IsActive ? "Active" : "Inactive",
            primaryContactName = firstUser is null ? "" : $"{firstUser.FirstName} {firstUser.LastName}",
            email              = firstUser?.Email,
            isActive           = t.IsActive,
            userCount          = t.Users.Count,
            activeUserCount    = t.Users.Count(u => u.IsActive),
            orgCount           = t.Organizations.Count,
            linkedOrgCount     = t.Organizations.Count,
            createdAtUtc       = t.CreatedAtUtc,
            updatedAtUtc       = t.UpdatedAtUtc,
            productEntitlements = entitlements,
        });
    }

    private static async Task<IResult> UpdateEntitlement(
        Guid id,
        IdentityDbContext db,
        EntitlementRequest body)
    {
        var tenant = await db.Tenants
            .Include(t => t.TenantProducts)
                .ThenInclude(tp => tp.Product)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null) return Results.NotFound();

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Code == body.ProductCode);

        if (product is null)
            return Results.NotFound(new { error = $"Product '{body.ProductCode}' not found." });

        var existing = tenant.TenantProducts.FirstOrDefault(tp => tp.ProductId == product.Id);

        if (existing is null)
        {
            if (body.Enabled)
            {
                var tp = Identity.Domain.TenantProduct.Create(tenant.Id, product.Id);
                db.Set<Identity.Domain.TenantProduct>().Add(tp);
            }
        }
        else
        {
            // Toggle — update via raw SQL since TenantProduct fields are private
            if (!body.Enabled)
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE TenantProducts SET IsEnabled = 0 WHERE TenantId = {0} AND ProductId = {1}",
                    tenant.Id, product.Id);
            else
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE TenantProducts SET IsEnabled = 1, EnabledAtUtc = {0} WHERE TenantId = {1} AND ProductId = {2}",
                    DateTime.UtcNow, tenant.Id, product.Id);
        }

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            tenantId    = tenant.Id,
            productCode = body.ProductCode,
            enabled     = body.Enabled,
            status      = body.Enabled ? "Active" : "Disabled",
        });
    }

    // =========================================================================
    // USERS
    // =========================================================================

    private static async Task<IResult> ListUsers(
        IdentityDbContext db,
        int    page     = 1,
        int    pageSize = 20,
        string search   = "",
        string tenantId = "")
    {
        var q = db.Users
            .Include(u => u.Tenant)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u =>
                u.Email.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search));

        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var tid))
            q = q.Where(u => u.TenantId == tid);

        var total = await q.CountAsync();

        var users = await q
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                id         = u.Id,
                firstName  = u.FirstName,
                lastName   = u.LastName,
                email      = u.Email,
                role       = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault() ?? "User",
                status     = u.IsActive ? "Active" : "Inactive",
                tenantId   = u.TenantId,
                tenantCode = u.Tenant.Code,
                createdAtUtc = u.CreatedAtUtc,
            })
            .ToListAsync();

        return Results.Ok(new
        {
            items      = users,
            totalCount = total,
            page,
            pageSize,
        });
    }

    private static async Task<IResult> GetUser(Guid id, IdentityDbContext db)
    {
        var u = await db.Users
            .Include(u => u.Tenant)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (u is null) return Results.NotFound();

        return Results.Ok(new
        {
            id                = u.Id,
            firstName         = u.FirstName,
            lastName          = u.LastName,
            email             = u.Email,
            role              = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault() ?? "User",
            status            = u.IsActive ? "Active" : "Inactive",
            tenantId          = u.TenantId,
            tenantCode        = u.Tenant.Code,
            tenantDisplayName = u.Tenant.Name,
            createdAtUtc      = u.CreatedAtUtc,
            updatedAtUtc      = u.UpdatedAtUtc,
            isLocked          = false,
        });
    }

    // =========================================================================
    // ROLES
    // =========================================================================

    private static async Task<IResult> ListRoles(
        IdentityDbContext db,
        int page     = 1,
        int pageSize = 20)
    {
        var total = await db.Roles.CountAsync();

        var roles = await db.Roles
            .Include(r => r.UserRoles)
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                id          = r.Id,
                name        = r.Name,
                description = r.Description ?? "",
                userCount   = r.UserRoles.Count,
                permissions = new string[] { },
            })
            .ToListAsync();

        return Results.Ok(new
        {
            items      = roles,
            totalCount = total,
            page,
            pageSize,
        });
    }

    private static async Task<IResult> GetRole(Guid id, IdentityDbContext db)
    {
        var r = await db.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (r is null) return Results.NotFound();

        return Results.Ok(new
        {
            id                  = r.Id,
            name                = r.Name,
            description         = r.Description ?? "",
            userCount           = r.UserRoles.Count,
            permissions         = new string[] { },
            resolvedPermissions = new object[] { },
            createdAtUtc        = r.CreatedAtUtc,
            updatedAtUtc        = r.UpdatedAtUtc,
        });
    }

    // =========================================================================
    // AUDIT LOGS
    // =========================================================================

    private static async Task<IResult> ListAudit(
        IdentityDbContext db,
        int    page       = 1,
        int    pageSize   = 20,
        string search     = "",
        string entityType = "",
        string actorType  = "")
    {
        var q = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(a =>
                a.Action.Contains(search)     ||
                a.EntityId.Contains(search)   ||
                a.ActorName.Contains(search));

        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(a => a.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(actorType))
            q = q.Where(a => a.ActorType == actorType);

        var total = await q.CountAsync();

        var raw = await q
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.ActorName,
                a.ActorType,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.MetadataJson,
                a.CreatedAtUtc,
            })
            .ToListAsync();

        var logs = raw.Select(a => new
        {
            id           = a.Id,
            actorName    = a.ActorName,
            actorType    = a.ActorType,
            action       = a.Action,
            entityType   = a.EntityType,
            entityId     = a.EntityId,
            metadata     = a.MetadataJson is not null
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.MetadataJson)
                : null,
            createdAtUtc = a.CreatedAtUtc,
        }).ToList();

        return Results.Ok(new
        {
            items      = logs,
            totalCount = total,
            page,
            pageSize,
        });
    }

    // =========================================================================
    // PLATFORM SETTINGS  (static seed — no DB table yet)
    // =========================================================================

    private static readonly List<PlatformSettingDto> _settings =
    [
        new("maintenance_mode",       "Maintenance Mode",         false, "boolean", "Put the platform into maintenance mode. All non-admin users will see a maintenance page.", false),
        new("max_users_per_tenant",   "Max Users Per Tenant",     100,   "number",  "Maximum number of users allowed per tenant. 0 = unlimited.",                              true),
        new("session_timeout_minutes","Session Timeout (minutes)", 60,   "number",  "Idle session timeout in minutes.",                                                        true),
        new("allow_self_registration","Allow Self-Registration",  false, "boolean", "Allow users to register without an invitation.",                                          true),
        new("default_product_code",   "Default Product",          "SynqFund", "string", "Product assigned to new tenants by default.",                                        true),
        new("support_email",          "Support Email",            "support@legalsynq.com", "string", "Email address displayed in the support footer.",                        true),
    ];

    private static IResult ListSettings()
    {
        return Results.Ok(new
        {
            items      = _settings,
            totalCount = _settings.Count,
            page       = 1,
            pageSize   = _settings.Count,
        });
    }

    private static IResult UpdateSetting(string key, SettingUpdateRequest body)
    {
        var setting = _settings.FirstOrDefault(s => s.key == key);
        if (setting is null) return Results.NotFound();

        return Results.Ok(setting with { value = body.Value });
    }

    // =========================================================================
    // SUPPORT CASES  (stub — no DB table yet; returns empty paged list)
    // =========================================================================

    private static IResult ListSupport(int page = 1, int pageSize = 20)
    {
        return Results.Ok(new
        {
            items      = Array.Empty<object>(),
            totalCount = 0,
            page,
            pageSize,
        });
    }

    private static IResult GetSupport(string id) =>
        Results.NotFound(new { error = "Support case not found." });

    private static IResult CreateSupport(CreateSupportRequest body) =>
        Results.Created("/api/admin/support/stub", new
        {
            id          = Guid.NewGuid(),
            title       = body.Title,
            status      = "Open",
            priority    = body.Priority ?? "Medium",
            category    = body.Category ?? "General",
            createdAtUtc = DateTime.UtcNow,
            updatedAtUtc = DateTime.UtcNow,
        });

    private static IResult AddSupportNote(string id, SupportNoteRequest body) =>
        Results.Ok(new
        {
            id           = Guid.NewGuid(),
            caseId       = id,
            message      = body.Message,
            createdBy    = "admin",
            createdAtUtc = DateTime.UtcNow,
        });

    private static IResult UpdateSupportStatus(string id, SupportStatusRequest body) =>
        Results.Ok(new
        {
            id           = id,
            status       = body.Status,
            updatedAtUtc = DateTime.UtcNow,
        });

    // ── Request / response DTOs (private, scoped to AdminEndpoints) ─────────

    private record EntitlementRequest(string ProductCode, bool Enabled);
    private record SettingUpdateRequest(object Value);
    private record CreateSupportRequest(string Title, string? Priority, string? Category);
    private record SupportNoteRequest(string Message);
    private record SupportStatusRequest(string Status);
    private record PlatformSettingDto(
        string  key,
        string  label,
        object  value,
        string  type,
        string  description,
        bool    editable);
}
