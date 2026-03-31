using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
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
        routes.MapPatch("/api/admin/tenants/{id:guid}/session-settings", UpdateTenantSessionSettings);

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

        // ── LSCC-010: Provider auto-provisioning — minimal org creation ──────
        // Internal service-to-service endpoint.  Token-gated at the gateway.
        // Creates a minimal PROVIDER Organization for a CareConnect provider.
        // Idempotent: returns the existing org if already provisioned.
        routes.MapPost("/api/admin/organizations",          AdminEndpointsLscc010.CreateProviderOrganization);
        routes.MapGet("/api/admin/organizations/{id:guid}", AdminEndpointsLscc010.GetOrganizationById);

        // ── Platform Foundation — Phase 1-6 ──────────────────────────────
        routes.MapGet("/api/admin/organization-types",             ListOrganizationTypes);
        routes.MapGet("/api/admin/organization-types/{id:guid}",   GetOrganizationType);

        routes.MapGet("/api/admin/relationship-types",             ListRelationshipTypes);
        routes.MapGet("/api/admin/relationship-types/{id:guid}",   GetRelationshipType);

        routes.MapGet("/api/admin/organization-relationships",     ListOrganizationRelationships);
        routes.MapGet("/api/admin/organization-relationships/{id:guid}", GetOrganizationRelationship);
        routes.MapPost("/api/admin/organization-relationships",    CreateOrganizationRelationship);
        routes.MapDelete("/api/admin/organization-relationships/{id:guid}", DeactivateOrganizationRelationship);

        routes.MapGet("/api/admin/product-org-type-rules",          ListProductOrgTypeRules);
        // Two URL variants served by the same handler — client uses the short form.
        routes.MapGet("/api/admin/product-relationship-type-rules", ListProductRelationshipTypeRules);
        routes.MapGet("/api/admin/product-rel-type-rules",          ListProductRelationshipTypeRules);

        // ── Legacy coverage (Phase G) ────────────────────────────────────────
        routes.MapGet("/api/admin/legacy-coverage", GetLegacyCoverage);

        // ── Platform readiness summary (Phase 8) ─────────────────────────────
        routes.MapGet("/api/admin/platform-readiness", GetPlatformReadiness);

        // ── User lifecycle ────────────────────────────────────────────────────
        // Step 27 (Phase B): user deactivation — emits identity.user.deactivated.
        routes.MapPatch("/api/admin/users/{id:guid}/deactivate",            DeactivateUser);

        // ── Role assignment ───────────────────────────────────────────────────
        routes.MapPost("/api/admin/users/{id:guid}/roles",                  AssignRole);
        routes.MapDelete("/api/admin/users/{id:guid}/roles/{roleId:guid}",  RevokeRole);

        // Phase I: scoped role summary for a user (non-global scope visibility)
        routes.MapGet("/api/admin/users/{id:guid}/scoped-roles",            GetScopedRoles);

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
            id                    = t.Id,
            code                  = t.Code,
            displayName           = t.Name,
            type                  = "LawFirm",
            status                = t.IsActive ? "Active" : "Inactive",
            primaryContactName    = firstUser is null ? "" : $"{firstUser.FirstName} {firstUser.LastName}",
            email                 = firstUser?.Email,
            isActive              = t.IsActive,
            userCount             = t.Users.Count,
            activeUserCount       = t.Users.Count(u => u.IsActive),
            orgCount              = t.Organizations.Count,
            linkedOrgCount        = t.Organizations.Count,
            createdAtUtc          = t.CreatedAtUtc,
            updatedAtUtc          = t.UpdatedAtUtc,
            sessionTimeoutMinutes = t.SessionTimeoutMinutes,
            productEntitlements   = entitlements,
        });
    }

    private static async Task<IResult> UpdateTenantSessionSettings(
        Guid id,
        IdentityDbContext db,
        SessionSettingsRequest body)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return Results.NotFound();

        try
        {
            tenant.SetSessionTimeout(body.SessionTimeoutMinutes);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.Problem(ex.Message, statusCode: 400);
        }

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            tenantId              = tenant.Id,
            sessionTimeoutMinutes = tenant.SessionTimeoutMinutes,
            updatedAtUtc          = tenant.UpdatedAtUtc,
        });
    }

    private static async Task<IResult> UpdateEntitlement(
        Guid id,
        IdentityDbContext db,
        EntitlementRequest body,
        IAuditEventClient auditClient)
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

        // Canonical audit — fire-and-observe, never gates the response.
        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "platform.admin.tenant.entitlement.updated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = auditNow,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = id.ToString() },
            Actor         = new AuditEventActorDto { Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "Tenant", Id = id.ToString() },
            Action        = "EntitlementUpdated",
            Description   = $"Product entitlement '{body.ProductCode}' {(body.Enabled ? "enabled" : "disabled")} for tenant {id}.",
            After         = JsonSerializer.Serialize(new { productCode = body.ProductCode, enabled = body.Enabled }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "platform.admin.tenant.entitlement.updated", id.ToString(), body.ProductCode),
            Tags = ["entitlement", "tenant-admin"],
        });

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
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u =>
                u.Email.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search));

        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var tid))
            q = q.Where(u => u.TenantId == tid);

        var total = await q.CountAsync();

        // Step 6 Phase B: role resolved via ScopedRoleAssignments (GLOBAL-scoped, primary).
        // Correlated subquery; EF Core translates to a single SQL query.
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
                role       = db.ScopedRoleAssignments
                               .Where(s => s.UserId == u.Id && s.IsActive
                                        && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
                               .Select(s => s.Role!.Name)
                               .FirstOrDefault() ?? "User",
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
        // Step 6 Phase B: ScopedRoleAssignments (GLOBAL) is the primary role source.
        var u = await db.Users
            .Include(u => u.Tenant)
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global))
                .ThenInclude(s => s.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (u is null) return Results.NotFound();

        return Results.Ok(new
        {
            id                = u.Id,
            firstName         = u.FirstName,
            lastName          = u.LastName,
            email             = u.Email,
            role              = u.ScopedRoleAssignments.Select(s => s.Role.Name).FirstOrDefault() ?? "User",
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
    // USER LIFECYCLE
    // =========================================================================

    /// <summary>
    /// PATCH /api/admin/users/{id}/deactivate
    ///
    /// Sets the user's IsActive flag to false and emits the canonical
    /// identity.user.deactivated audit event (HIPAA-required lifecycle record).
    ///
    /// Idempotent: if the user is already inactive, returns 204 without re-emitting.
    /// Returns 404 if the user does not exist.
    /// </summary>
    private static async Task<IResult> DeactivateUser(
        Guid              id,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();

        // Deactivate() is idempotent — returns false if already inactive.
        var changed = user.Deactivate();
        if (!changed) return Results.NoContent();

        await db.SaveChangesAsync(ct);

        // Canonical audit: identity.user.deactivated — fire-and-observe.
        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.deactivated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = user.TenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Type = ActorType.System,
                Name = "admin-api",
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "UserDeactivated",
            Description = $"User '{user.Email}' deactivated in tenant {user.TenantId}.",
            Before      = JsonSerializer.Serialize(new { isActive = true,  email = user.Email }),
            After       = JsonSerializer.Serialize(new { isActive = false, email = user.Email }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.deactivated", user.Id.ToString()),
            Tags = ["user-management", "lifecycle", "deactivation"],
        });

        return Results.NoContent();
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

        // Step 6 Phase B: userCount via ScopedRoleAssignments (GLOBAL-scoped, primary).
        var roles = await db.Roles
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                id          = r.Id,
                name        = r.Name,
                description = r.Description ?? "",
                userCount   = db.ScopedRoleAssignments.Count(
                                  s => s.RoleId == r.Id && s.IsActive
                                    && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global),
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
            .FirstOrDefaultAsync(r => r.Id == id);

        if (r is null) return Results.NotFound();

        // Step 6 Phase B: userCount via ScopedRoleAssignments (GLOBAL-scoped, primary).
        var userCount = await db.ScopedRoleAssignments
            .CountAsync(s => s.RoleId == id && s.IsActive
                          && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global);

        return Results.Ok(new
        {
            id                  = r.Id,
            name                = r.Name,
            description         = r.Description ?? "",
            userCount,
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

    // =========================================================================
    // ORGANIZATION TYPES  (Phase 1)
    // =========================================================================

    private static async Task<IResult> ListOrganizationTypes(IdentityDbContext db)
    {
        var items = await db.OrganizationTypes
            .OrderBy(ot => ot.DisplayName)
            .Select(ot => new
            {
                id          = ot.Id,
                code        = ot.Code,
                displayName = ot.DisplayName,
                description = ot.Description,
                isSystem    = ot.IsSystem,
                isActive    = ot.IsActive,
                createdAtUtc = ot.CreatedAtUtc,
            })
            .ToListAsync();

        return Results.Ok(new { items, totalCount = items.Count });
    }

    private static async Task<IResult> GetOrganizationType(Guid id, IdentityDbContext db)
    {
        var ot = await db.OrganizationTypes.FirstOrDefaultAsync(x => x.Id == id);
        if (ot is null) return Results.NotFound();

        return Results.Ok(new
        {
            id          = ot.Id,
            code        = ot.Code,
            displayName = ot.DisplayName,
            description = ot.Description,
            isSystem    = ot.IsSystem,
            isActive    = ot.IsActive,
            createdAtUtc = ot.CreatedAtUtc,
        });
    }

    // =========================================================================
    // RELATIONSHIP TYPES  (Phase 2)
    // =========================================================================

    private static async Task<IResult> ListRelationshipTypes(IdentityDbContext db)
    {
        var items = await db.RelationshipTypes
            .OrderBy(rt => rt.DisplayName)
            .Select(rt => new
            {
                id            = rt.Id,
                code          = rt.Code,
                displayName   = rt.DisplayName,
                description   = rt.Description,
                isDirectional = rt.IsDirectional,
                isSystem      = rt.IsSystem,
                isActive      = rt.IsActive,
                createdAtUtc  = rt.CreatedAtUtc,
            })
            .ToListAsync();

        return Results.Ok(new { items, totalCount = items.Count });
    }

    private static async Task<IResult> GetRelationshipType(Guid id, IdentityDbContext db)
    {
        var rt = await db.RelationshipTypes.FirstOrDefaultAsync(x => x.Id == id);
        if (rt is null) return Results.NotFound();

        return Results.Ok(new
        {
            id            = rt.Id,
            code          = rt.Code,
            displayName   = rt.DisplayName,
            description   = rt.Description,
            isDirectional = rt.IsDirectional,
            isSystem      = rt.IsSystem,
            isActive      = rt.IsActive,
            createdAtUtc  = rt.CreatedAtUtc,
        });
    }

    // =========================================================================
    // ORGANIZATION RELATIONSHIPS  (Phase 2)
    // =========================================================================

    private static async Task<IResult> ListOrganizationRelationships(
        IdentityDbContext db,
        int    page       = 1,
        int    pageSize   = 20,
        string tenantId   = "",
        string sourceOrgId = "",
        bool   activeOnly = true)
    {
        var q = db.OrganizationRelationships
            .Include(r => r.SourceOrganization)
            .Include(r => r.TargetOrganization)
            .Include(r => r.RelationshipType)
            .AsQueryable();

        if (activeOnly)
            q = q.Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var tid))
            q = q.Where(r => r.TenantId == tid);

        if (!string.IsNullOrWhiteSpace(sourceOrgId) && Guid.TryParse(sourceOrgId, out var sid))
            q = q.Where(r => r.SourceOrganizationId == sid);

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                id                   = r.Id,
                tenantId             = r.TenantId,
                sourceOrganizationId = r.SourceOrganizationId,
                sourceOrgName        = r.SourceOrganization.DisplayName ?? r.SourceOrganization.Name,
                targetOrganizationId = r.TargetOrganizationId,
                targetOrgName        = r.TargetOrganization.DisplayName ?? r.TargetOrganization.Name,
                relationshipTypeId   = r.RelationshipTypeId,
                relationshipTypeCode = r.RelationshipType.Code,
                relationshipTypeDisplayName = r.RelationshipType.DisplayName,
                productId            = r.ProductId,
                isActive             = r.IsActive,
                establishedAtUtc     = r.EstablishedAtUtc,
                createdAtUtc         = r.CreatedAtUtc,
            })
            .ToListAsync();

        return Results.Ok(new { items, totalCount = total, page, pageSize });
    }

    private static async Task<IResult> GetOrganizationRelationship(Guid id, IdentityDbContext db)
    {
        var r = await db.OrganizationRelationships
            .Include(x => x.SourceOrganization)
            .Include(x => x.TargetOrganization)
            .Include(x => x.RelationshipType)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r is null) return Results.NotFound();

        return Results.Ok(new
        {
            id                   = r.Id,
            tenantId             = r.TenantId,
            sourceOrganizationId = r.SourceOrganizationId,
            sourceOrgName        = r.SourceOrganization.DisplayName ?? r.SourceOrganization.Name,
            targetOrganizationId = r.TargetOrganizationId,
            targetOrgName        = r.TargetOrganization.DisplayName ?? r.TargetOrganization.Name,
            relationshipTypeId   = r.RelationshipTypeId,
            relationshipTypeCode = r.RelationshipType.Code,
            productId            = r.ProductId,
            isActive             = r.IsActive,
            establishedAtUtc     = r.EstablishedAtUtc,
            createdAtUtc         = r.CreatedAtUtc,
            updatedAtUtc         = r.UpdatedAtUtc,
        });
    }

    private static async Task<IResult> CreateOrganizationRelationship(
        CreateOrgRelationshipRequest body,
        IdentityDbContext db,
        IAuditEventClient auditClient)
    {
        // Validate orgs exist
        var sourceOrg = await db.Organizations.FirstOrDefaultAsync(o => o.Id == body.SourceOrganizationId);
        if (sourceOrg is null)
            return Results.NotFound(new { error = "Source organization not found." });

        var targetOrg = await db.Organizations.FirstOrDefaultAsync(o => o.Id == body.TargetOrganizationId);
        if (targetOrg is null)
            return Results.NotFound(new { error = "Target organization not found." });

        var relType = await db.RelationshipTypes.FirstOrDefaultAsync(rt => rt.Id == body.RelationshipTypeId);
        if (relType is null)
            return Results.NotFound(new { error = "Relationship type not found." });

        var existing = await db.OrganizationRelationships.FirstOrDefaultAsync(r =>
            r.TenantId == sourceOrg.TenantId &&
            r.SourceOrganizationId == body.SourceOrganizationId &&
            r.TargetOrganizationId == body.TargetOrganizationId &&
            r.RelationshipTypeId == body.RelationshipTypeId);

        if (existing is not null)
            return Results.Conflict(new { error = "Relationship already exists." });

        var rel = Identity.Domain.OrganizationRelationship.Create(
            tenantId             : sourceOrg.TenantId,
            sourceOrganizationId : body.SourceOrganizationId,
            targetOrganizationId : body.TargetOrganizationId,
            relationshipTypeId   : body.RelationshipTypeId,
            productId            : body.ProductId);

        db.OrganizationRelationships.Add(rel);
        await db.SaveChangesAsync();

        // Canonical audit — fire-and-observe, never gates the response.
        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "platform.admin.org.relationship.created",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = sourceOrg.TenantId.ToString() },
            Actor         = new AuditEventActorDto { Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "OrganizationRelationship", Id = rel.Id.ToString() },
            Action        = "RelationshipCreated",
            Description   = $"Organization relationship created: {body.SourceOrganizationId} → {body.TargetOrganizationId} ({relType.DisplayName}).",
            After         = JsonSerializer.Serialize(new { id = rel.Id, body.SourceOrganizationId, body.TargetOrganizationId, body.RelationshipTypeId, body.ProductId }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "platform.admin.org.relationship.created", rel.Id.ToString()),
            Tags = ["org-relationship", "admin"],
        });

        return Results.Created($"/api/admin/organization-relationships/{rel.Id}", new
        {
            id                   = rel.Id,
            tenantId             = rel.TenantId,
            sourceOrganizationId = rel.SourceOrganizationId,
            targetOrganizationId = rel.TargetOrganizationId,
            relationshipTypeId   = rel.RelationshipTypeId,
            productId            = rel.ProductId,
            isActive             = rel.IsActive,
            establishedAtUtc     = rel.EstablishedAtUtc,
        });
    }

    private static async Task<IResult> DeactivateOrganizationRelationship(Guid id, IdentityDbContext db, IAuditEventClient auditClient)
    {
        var rel = await db.OrganizationRelationships.FirstOrDefaultAsync(r => r.Id == id);
        if (rel is null) return Results.NotFound();

        rel.Deactivate();
        await db.SaveChangesAsync();

        // Canonical audit — fire-and-observe, never gates the response.
        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "platform.admin.org.relationship.deactivated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = auditNow,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = rel.TenantId.ToString() },
            Actor         = new AuditEventActorDto { Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "OrganizationRelationship", Id = id.ToString() },
            Action        = "RelationshipDeactivated",
            Description   = $"Organization relationship {id} deactivated.",
            Before        = JsonSerializer.Serialize(new { id, isActive = true }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "platform.admin.org.relationship.deactivated", id.ToString()),
            Tags = ["org-relationship", "admin"],
        });

        return Results.Ok(new { id = rel.Id, isActive = false });
    }

    // =========================================================================
    // PRODUCT ORG-TYPE RULES  (Phase 3)
    // =========================================================================

    private static async Task<IResult> ListProductOrgTypeRules(IdentityDbContext db)
    {
        // Response: plain array — client does Array.isArray(raw) check.
        // Field names must match api-mappers.ts mapProductOrgTypeRule camelCase keys.
        var items = await db.ProductOrganizationTypeRules
            .Include(r => r.Product)
            .Include(r => r.ProductRole)
            .Include(r => r.OrganizationType)
            .Where(r => r.IsActive)
            .OrderBy(r => r.Product.Code)
                .ThenBy(r => r.ProductRole.Code)
            .Select(r => new
            {
                id                   = r.Id,
                productId            = r.ProductId,
                productCode          = r.Product.Code,
                productRoleId        = r.ProductRoleId,
                productRoleCode      = r.ProductRole.Code,
                productRoleName      = r.ProductRole.Name,
                organizationTypeId   = r.OrganizationTypeId,
                organizationTypeCode = r.OrganizationType.Code,         // mapper expects this name
                organizationTypeName = r.OrganizationType.DisplayName,
                isActive             = r.IsActive,
                createdAtUtc         = r.CreatedAtUtc,
            })
            .ToListAsync();

        return Results.Ok(items);   // plain array, not { items, totalCount }
    }

    // =========================================================================
    // PRODUCT RELATIONSHIP-TYPE RULES  (Phase 2)
    // =========================================================================

    private static async Task<IResult> ListProductRelationshipTypeRules(IdentityDbContext db)
    {
        // Registered under both /product-relationship-type-rules (canonical) and
        // /product-rel-type-rules (short alias used by the control-center client).
        var items = await db.ProductRelationshipTypeRules
            .Include(r => r.Product)
            .Include(r => r.RelationshipType)
            .Where(r => r.IsActive)
            .OrderBy(r => r.Product.Code)
                .ThenBy(r => r.RelationshipType.Code)
            .Select(r => new
            {
                id                   = r.Id,
                productId            = r.ProductId,
                productCode          = r.Product.Code,
                relationshipTypeId   = r.RelationshipTypeId,
                relationshipTypeCode = r.RelationshipType.Code,
                relationshipTypeName = r.RelationshipType.DisplayName,
                isActive             = r.IsActive,
                createdAtUtc         = r.CreatedAtUtc,
            })
            .ToListAsync();

        return Results.Ok(items);   // plain array, not { items, totalCount }
    }

    // =========================================================================
    // LEGACY COVERAGE  (Step 4 / Phase F)
    // =========================================================================

    /// <summary>
    /// GET /api/admin/legacy-coverage
    ///
    /// Returns a point-in-time snapshot of the two Phase F migration paths:
    ///
    ///   1. Eligibility rules (Phase F COMPLETE):
    ///      - EligibleOrgType column dropped (migration 20260330200003).
    ///      - legacyStringOnly = 0 always. withBothPaths = 0 always.
    ///      - All 7 restricted ProductRoles use ProductOrganizationTypeRules exclusively.
    ///      - dbCoveragePct reflects OrgTypeRule coverage over all restricted roles.
    ///
    ///   2. UserRoles → ScopedRoleAssignment dual-write adoption (ongoing):
    ///      - Tracks users with legacy UserRole records vs. GLOBAL ScopedRoleAssignments.
    ///      - Gap = usersWithLegacyRoles − usersWithScopedRoles (should reach 0 after backfill).
    ///      - Migration 20260330200002 backfills UserRoles → ScopedRoleAssignments.
    ///
    /// Used by the /legacy-coverage control center page to track cutover progress.
    /// </summary>
    private static async Task<IResult> GetLegacyCoverage(IdentityDbContext db)
    {
        // ── 1. Eligibility rules — Phase F complete ───────────────────────────
        // EligibleOrgType column removed; all eligibility driven by OrgTypeRules.

        var allActiveRoles = await db.ProductRoles
            .Where(pr => pr.IsActive)
            .Select(pr => new { pr.Id, pr.Code })
            .ToListAsync();

        // ProductRole IDs that have at least one active OrgTypeRule
        var rolesWithDbRuleList = await db.ProductOrganizationTypeRules
            .Where(r => r.IsActive)
            .Select(r => r.ProductRoleId)
            .Distinct()
            .ToListAsync();
        var rolesWithDbRules = new HashSet<Guid>(rolesWithDbRuleList);

        int withDbRuleOnly = 0;
        int unrestricted   = 0;

        foreach (var pr in allActiveRoles)
        {
            if (rolesWithDbRules.Contains(pr.Id)) withDbRuleOnly++;
            else                                  unrestricted++;
        }

        // Phase F: these are permanently 0 — column dropped, path retired.
        const int withBothPaths    = 0;
        const int legacyStringOnly = 0;

        double eligibilityCoverage = allActiveRoles.Count > 0
            ? Math.Round((double)withDbRuleOnly / allActiveRoles.Count * 100, 1)
            : 100.0;

        // ── 2. ScopedRoleAssignment adoption — Phase G complete ──────────────

        // Phase G: UserRoles table dropped. Count authoritative scoped assignments.
        var usersWithScopedRole = await db.ScopedRoleAssignments
            .Where(s => s.IsActive && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync();

        var totalScopedAssignments = await db.ScopedRoleAssignments
            .CountAsync(s => s.IsActive && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global);

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,

            eligibilityRules = new
            {
                totalActiveProductRoles  = allActiveRoles.Count,
                withDbRuleOnly,
                withBothPaths,           // Phase F+G: always 0 — EligibleOrgType column dropped
                legacyStringOnly,        // Phase F+G: always 0 — EligibleOrgType column dropped
                unrestricted,
                dbCoveragePct            = eligibilityCoverage,
            },

            roleAssignments = new
            {
                usersWithScopedRoles        = usersWithScopedRole,
                totalActiveScopedAssignments = totalScopedAssignments,
                // Phase G: UserRoles table retired. Gap metric no longer applicable.
                userRolesRetired            = true,
                dualWriteCoveragePct        = 100.0,
            },
        });
    }

    // =========================================================================
    // ROLE ASSIGNMENT  (Step 5 — dual-write admin endpoints)
    // =========================================================================

    /// <summary>
    /// POST /api/admin/users/{id}/roles
    ///
    /// Assigns a role to a user.
    ///
    /// Phase G: UserRoles table dropped — ScopedRoleAssignments is the sole store.
    /// Phase I: extended to support non-GLOBAL scope types.
    ///
    /// Body: { "roleId": "guid", "scopeType": "GLOBAL|ORGANIZATION|PRODUCT|RELATIONSHIP|TENANT",
    ///         "organizationId"?: "guid", "productId"?: "guid",
    ///         "organizationRelationshipId"?: "guid" }
    ///
    /// scopeType defaults to GLOBAL when omitted (backward compatible).
    /// ORGANIZATION scope requires organizationId.
    /// PRODUCT scope requires productId.
    /// RELATIONSHIP scope requires organizationRelationshipId.
    ///
    /// Returns 201 Created on success, 400 for scope validation errors,
    /// 404 if user or role not found, 409 Conflict if the same scoped assignment exists.
    /// </summary>
    private static async Task<IResult> AssignRole(
        Guid                 id,
        AssignRoleRequest    body,
        IdentityDbContext    db,
        IAuditEventClient    auditClient,
        HttpContext          ctx)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        var role = await db.Roles.FindAsync(body.RoleId);
        if (role is null) return Results.NotFound(new { error = $"Role '{body.RoleId}' not found." });

        // Phase I: validate scope context
        var scopeType = body.ScopeType ?? ScopedRoleAssignment.ScopeTypes.Global;
        if (!ScopedRoleAssignment.ScopeTypes.IsValid(scopeType))
            return Results.BadRequest(new { error = $"Invalid ScopeType '{scopeType}'. Valid values: {string.Join(", ", ScopedRoleAssignment.ScopeTypes.All)}." });

        if (scopeType == ScopedRoleAssignment.ScopeTypes.Organization && !body.OrganizationId.HasValue)
            return Results.BadRequest(new { error = "OrganizationId is required when ScopeType is ORGANIZATION." });

        if (scopeType == ScopedRoleAssignment.ScopeTypes.Product && !body.ProductId.HasValue)
            return Results.BadRequest(new { error = "ProductId is required when ScopeType is PRODUCT." });

        if (scopeType == ScopedRoleAssignment.ScopeTypes.Relationship && !body.OrganizationRelationshipId.HasValue)
            return Results.BadRequest(new { error = "OrganizationRelationshipId is required when ScopeType is RELATIONSHIP." });

        // Validate referenced entities for non-global scopes
        if (body.OrganizationId.HasValue)
        {
            var orgExists = await db.Organizations.AnyAsync(o => o.Id == body.OrganizationId.Value);
            if (!orgExists)
                return Results.NotFound(new { error = $"Organization '{body.OrganizationId.Value}' not found." });
        }
        if (body.ProductId.HasValue)
        {
            var productExists = await db.Products.AnyAsync(p => p.Id == body.ProductId.Value);
            if (!productExists)
                return Results.NotFound(new { error = $"Product '{body.ProductId.Value}' not found." });
        }
        if (body.OrganizationRelationshipId.HasValue)
        {
            var relExists = await db.OrganizationRelationships.AnyAsync(r => r.Id == body.OrganizationRelationshipId.Value && r.IsActive);
            if (!relExists)
                return Results.NotFound(new { error = $"Active OrganizationRelationship '{body.OrganizationRelationshipId.Value}' not found." });
        }

        // Conflict check: same user + same role + same scope type + same scope context
        var alreadyAssigned = await db.ScopedRoleAssignments
            .AnyAsync(s =>
                s.UserId     == id &&
                s.RoleId     == body.RoleId &&
                s.IsActive   &&
                s.ScopeType  == scopeType &&
                s.OrganizationId           == body.OrganizationId &&
                s.ProductId                == body.ProductId &&
                s.OrganizationRelationshipId == body.OrganizationRelationshipId);
        if (alreadyAssigned)
            return Results.Conflict(new { error = "An identical scoped role assignment already exists for this user." });

        var now = DateTime.UtcNow;

        // Phase G/I: single write — ScopedRoleAssignment only.
        var sra = ScopedRoleAssignment.Create(
            userId:                    id,
            roleId:                    body.RoleId,
            scopeType:                 scopeType,
            tenantId:                  user.TenantId,
            organizationId:            body.OrganizationId,
            organizationRelationshipId: body.OrganizationRelationshipId,
            productId:                 body.ProductId,
            assignedByUserId:          body.AssignedByUserId);
        db.ScopedRoleAssignments.Add(sra);

        await db.SaveChangesAsync();

        // Canonical audit — fire-and-observe, never gates the response.
        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.role.assigned",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Scope = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = user.TenantId.ToString() },
            Actor = new AuditEventActorDto { Id = body.AssignedByUserId?.ToString(), Type = ActorType.User },
            Entity = new AuditEventEntityDto { Type = "User", Id = id.ToString() },
            Action      = "RoleAssigned",
            Description = $"Role '{role.Name}' ({scopeType}) assigned to user {id}.",
            After       = JsonSerializer.Serialize(new { roleId = body.RoleId, roleName = role.Name, scopeType, organizationId = body.OrganizationId }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.role.assigned", sra.Id.ToString()),
            Tags = ["role-management", "access-control"],
        });

        return Results.Created(
            $"/api/admin/users/{id}/roles/{body.RoleId}",
            new
            {
                assignmentId              = sra.Id,
                userId                    = id,
                roleId                    = body.RoleId,
                roleName                  = role.Name,
                scopeType                 = scopeType,
                organizationId            = body.OrganizationId,
                productId                 = body.ProductId,
                organizationRelationshipId = body.OrganizationRelationshipId,
                assignedAtUtc             = now,
            });
    }

    /// <summary>
    /// GET /api/admin/users/{id}/scoped-roles
    ///
    /// Phase I: returns all active scoped role assignments for a user, grouped by
    /// scope type.  Demonstrates real non-global scope visibility at the API layer.
    ///
    /// Returns 200 with the scoped role summary, 404 if the user is not found.
    /// </summary>
    private static async Task<IResult> GetScopedRoles(
        Guid                        id,
        IScopedAuthorizationService scopedAuth,
        IdentityDbContext            db,
        CancellationToken            ct)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == id, ct);
        if (!exists) return Results.NotFound(new { error = $"User '{id}' not found." });

        var summary = await scopedAuth.GetScopedRoleSummaryAsync(id, ct);

        return Results.Ok(new
        {
            userId      = summary.UserId,
            totalActive = summary.TotalActive,
            assignments = summary.Assignments.Select(a => new
            {
                assignmentId               = a.AssignmentId,
                roleName                   = a.RoleName,
                scopeType                  = a.ScopeType,
                organizationId             = a.OrganizationId,
                productId                  = a.ProductId,
                organizationRelationshipId = a.OrganizationRelationshipId,
                tenantId                   = a.TenantId,
            }),
            byScope = summary.Assignments
                .GroupBy(a => a.ScopeType)
                .ToDictionary(
                    g => g.Key.ToLowerInvariant(),
                    g => g.Count()),
        });
    }

    /// <summary>
    /// DELETE /api/admin/users/{id}/roles/{roleId}
    ///
    /// Revokes a role from a user.  Deactivates the GLOBAL ScopedRoleAssignment.
    ///
    /// Returns 204 No Content on success, 404 if user or assignment not found.
    /// </summary>
    private static async Task<IResult> RevokeRole(
        Guid              id,
        Guid              roleId,
        IdentityDbContext db,
        IAuditEventClient auditClient)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        // Phase G: deactivate the GLOBAL ScopedRoleAssignment (sole authoritative record).
        var sra = await db.ScopedRoleAssignments
            .Include(s => s.Role)
            .FirstOrDefaultAsync(s => s.UserId == id && s.RoleId == roleId
                                   && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global && s.IsActive);
        if (sra is null)
            return Results.NotFound(new { error = $"Role '{roleId}' is not assigned to user '{id}'." });

        var roleName = sra.Role?.Name ?? roleId.ToString();
        sra.Deactivate();

        await db.SaveChangesAsync();

        // Canonical audit — fire-and-observe, never gates the response.
        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.role.removed",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = auditNow,
            Scope = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = user.TenantId.ToString() },
            Actor = new AuditEventActorDto { Type = ActorType.User },
            Entity = new AuditEventEntityDto { Type = "User", Id = id.ToString() },
            Action      = "RoleRemoved",
            Description = $"Role '{roleName}' removed from user {id}.",
            Before      = JsonSerializer.Serialize(new { roleId, roleName }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.role.removed", id.ToString(), roleId.ToString()),
            Tags = ["role-management", "access-control"],
        });

        return Results.NoContent();
    }

    /// <summary>
    /// GET /api/admin/platform-readiness
    ///
    /// Returns a cross-domain readiness summary covering Phase G completion status,
    /// OrgType consistency, product-role eligibility coverage, and role assignment
    /// depth — for the platform operations dashboard.
    ///
    /// Returns 200 with the readiness payload (never 404/500 — issues surface as
    /// degraded/false flags inside the response so the dashboard always renders).
    /// </summary>
    private static async Task<IResult> GetPlatformReadiness(
        IdentityDbContext db,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // ── 1. Phase G completion ─────────────────────────────────────────────
        // Phase G removed UserRoles / UserRoleAssignments tables and established
        // ScopedRoleAssignments (GLOBAL scope) as the sole authoritative role source.
        var totalScopedActive   = await db.ScopedRoleAssignments.CountAsync(s => s.IsActive,              ct);
        var globalScopedActive  = await db.ScopedRoleAssignments.CountAsync(s => s.IsActive && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global, ct);
        var usersWithScopedRole = await db.ScopedRoleAssignments
            .Where(s => s.IsActive)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(ct);

        // ── 2. OrgType consistency ────────────────────────────────────────────
        var orgRows = await db.Organizations
            .Where(o => o.IsActive)
            .Select(o => new { o.OrganizationTypeId, o.OrgType })
            .ToListAsync(ct);

        var totalActiveOrgs       = orgRows.Count;
        var orgsWithTypeId        = orgRows.Count(o => o.OrganizationTypeId.HasValue);
        var orgsWithMissingTypeId = orgRows.Count(o => o.OrganizationTypeId == null && !string.IsNullOrWhiteSpace(o.OrgType));
        var orgsWithCodeMismatch  = orgRows.Count(o =>
        {
            if (o.OrganizationTypeId == null) return false;
            var code = OrgTypeMapper.TryResolveCode(o.OrganizationTypeId);
            return code is not null && !string.Equals(code, o.OrgType, StringComparison.OrdinalIgnoreCase);
        });
        var orgTypeConsistent = orgsWithMissingTypeId == 0 && orgsWithCodeMismatch == 0;

        // ── 3. ProductRole eligibility coverage ──────────────────────────────
        var totalActiveProductRoles = await db.ProductRoles.CountAsync(r => r.IsActive, ct);
        var productRolesWithOrgRule = await db.ProductOrganizationTypeRules
            .Where(r => r.IsActive)
            .Select(r => r.ProductRoleId)
            .Distinct()
            .CountAsync(ct);
        var productRolesUnrestricted = totalActiveProductRoles - productRolesWithOrgRule;
        var eligibilityCoveragePct   = totalActiveProductRoles == 0
            ? 100.0
            : Math.Round((double)productRolesWithOrgRule / totalActiveProductRoles * 100.0, 1);

        // ── 4. Org-relationship coverage ──────────────────────────────────────
        var totalOrgRelationships   = await db.OrganizationRelationships.CountAsync(ct);
        var activeOrgRelationships  = await db.OrganizationRelationships.CountAsync(r => r.IsActive, ct);

        // ── 5. Phase I: scoped assignments by scope type ──────────────────────
        // Shows how many active SRAs exist per scope level.  After Phase I,
        // non-GLOBAL counts above zero prove the schema is being exercised at runtime.
        var scopeTypeCounts = await db.ScopedRoleAssignments
            .Where(s => s.IsActive)
            .GroupBy(s => s.ScopeType)
            .Select(g => new { ScopeType = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int ScopeCount(string t) =>
            scopeTypeCounts.FirstOrDefault(g => g.ScopeType == t)?.Count ?? 0;

        var scopedGlobal       = ScopeCount(ScopedRoleAssignment.ScopeTypes.Global);
        var scopedOrg          = ScopeCount(ScopedRoleAssignment.ScopeTypes.Organization);
        var scopedProduct      = ScopeCount(ScopedRoleAssignment.ScopeTypes.Product);
        var scopedRelationship = ScopeCount(ScopedRoleAssignment.ScopeTypes.Relationship);
        var scopedTenant       = ScopeCount(ScopedRoleAssignment.ScopeTypes.Tenant);

        return Results.Ok(new
        {
            generatedAtUtc = now,

            phaseGCompletion = new
            {
                userRolesRetired              = true,      // migration 200004 executed — tables dropped
                soleRoleSourceIsSra           = true,
                totalActiveScopedAssignments  = totalScopedActive,
                globalScopedAssignments       = globalScopedActive,
                usersWithScopedRole,
            },

            orgTypeCoverage = new
            {
                totalActiveOrgs,
                orgsWithOrganizationTypeId = orgsWithTypeId,
                orgsWithMissingTypeId,
                orgsWithCodeMismatch,
                consistent                 = orgTypeConsistent,
                coveragePct                = totalActiveOrgs == 0
                    ? 100.0
                    : Math.Round((double)orgsWithTypeId / totalActiveOrgs * 100.0, 1),
            },

            productRoleEligibility = new
            {
                totalActiveProductRoles,
                withOrgTypeRule     = productRolesWithOrgRule,
                unrestricted        = productRolesUnrestricted,
                coveragePct         = eligibilityCoveragePct,
            },

            orgRelationships = new
            {
                total  = totalOrgRelationships,
                active = activeOrgRelationships,
            },

            // Phase I: active SRAs by scope type — non-zero org/product/relationship
            // values confirm that real non-global scope enforcement is in use.
            scopedAssignmentsByScope = new
            {
                global       = scopedGlobal,
                organization = scopedOrg,
                product      = scopedProduct,
                relationship = scopedRelationship,
                tenant       = scopedTenant,
            },
        });
    }

    // ── Request / response DTOs (private, scoped to AdminEndpoints) ─────────

    private record AssignRoleRequest(
        Guid    RoleId,
        Guid?   AssignedByUserId             = null,
        /// <summary>Defaults to GLOBAL when omitted. Valid: GLOBAL, ORGANIZATION, PRODUCT, RELATIONSHIP, TENANT.</summary>
        string? ScopeType                    = null,
        Guid?   OrganizationId               = null,
        Guid?   ProductId                    = null,
        Guid?   OrganizationRelationshipId   = null);
    private record EntitlementRequest(string ProductCode, bool Enabled);
    private record SessionSettingsRequest(int? SessionTimeoutMinutes);
    private record CreateOrgRelationshipRequest(
        Guid  SourceOrganizationId,
        Guid  TargetOrganizationId,
        Guid  RelationshipTypeId,
        Guid? ProductId);
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

/// <summary>
/// LSCC-010: Handler methods for the provider auto-provisioning org endpoints.
/// Separated as partial class extensions for file manageability.
/// </summary>
public static partial class AdminEndpointsLscc010
{
    // Deterministic org name that embeds the CareConnect provider ID for stable lookup.
    // Format: "{ProviderName} [cc:{providerCcId:D}]"
    // This is the idempotency key — the same provider always maps to the same org.
    private static string OrgName(string providerName, Guid providerCcId)
        => $"{providerName.Trim()} [cc:{providerCcId:D}]";

    /// <summary>
    /// POST /api/admin/organizations
    /// Creates a minimal PROVIDER Organization for a CareConnect provider.
    /// Idempotent — returns the existing org if one was already created for this provider.
    /// </summary>
    public static async Task<IResult> CreateProviderOrganization(
        CreateProviderOrgRequest body,
        IdentityDbContext        db,
        CancellationToken        ct)
    {
        if (body.TenantId   == Guid.Empty) return Results.BadRequest(new { error = "tenantId is required." });
        if (body.ProviderCcId == Guid.Empty) return Results.BadRequest(new { error = "providerCcId is required." });
        if (string.IsNullOrWhiteSpace(body.ProviderName)) return Results.BadRequest(new { error = "providerName is required." });

        // Validate tenant exists
        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == body.TenantId, ct);
        if (!tenantExists)
            return Results.NotFound(new { error = $"Tenant '{body.TenantId}' not found." });

        var name = OrgName(body.ProviderName, body.ProviderCcId);

        // Idempotency: look up existing org with this deterministic name under the tenant
        var existing = await db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.TenantId == body.TenantId
                                   && o.OrgType   == "PROVIDER"
                                   && o.Name      == name, ct);

        if (existing is not null)
        {
            return Results.Ok(new CreateProviderOrgResponse(existing.Id, existing.Name, IsNew: false));
        }

        // Create minimal PROVIDER org — no billing, no user setup, no domains
        var org = Organization.Create(
            tenantId:   body.TenantId,
            name:       name,
            orgType:    OrgType.Provider,
            displayName: body.ProviderName.Trim());

        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/admin/organizations/{org.Id}",
            new CreateProviderOrgResponse(org.Id, org.Name, IsNew: true));
    }

    /// <summary>
    /// GET /api/admin/organizations/{id}
    /// Returns a minimal org record by ID for verification/lookup.
    /// </summary>
    public static async Task<IResult> GetOrganizationById(
        Guid              id,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var org = await db.Organizations
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new { o.Id, o.TenantId, o.Name, o.OrgType, o.IsActive, o.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);

        return org is null ? Results.NotFound() : Results.Ok(org);
    }

    // Keep the request/response records accessible to the route registration above
    public record CreateProviderOrgRequest(
        Guid   TenantId,
        Guid   ProviderCcId,
        string ProviderName);

    private record CreateProviderOrgResponse(
        Guid   Id,
        string Name,
        bool   IsNew);
}
