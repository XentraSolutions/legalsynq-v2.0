using System.Security.Claims;
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
        routes.MapGet("/api/admin/tenants",           ListTenants);
        routes.MapPost("/api/admin/tenants",          CreateTenant);
        routes.MapGet("/api/admin/tenants/{id:guid}", GetTenant);
        routes.MapPost("/api/admin/tenants/{id:guid}/entitlements/{productCode}", UpdateEntitlement);
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

        // UIX-002: activate user
        routes.MapPost("/api/admin/users/{id:guid}/activate",               ActivateUser);

        // UIX-002: invite user
        routes.MapPost("/api/admin/users/invite",                           InviteUser);

        // UIX-002: resend invite
        routes.MapPost("/api/admin/users/{id:guid}/resend-invite",          ResendInvite);

        // ── Role assignment ───────────────────────────────────────────────────
        routes.MapPost("/api/admin/users/{id:guid}/roles",                  AssignRole);
        routes.MapDelete("/api/admin/users/{id:guid}/roles/{roleId:guid}",  RevokeRole);

        // Phase I: scoped role summary for a user (non-global scope visibility)
        routes.MapGet("/api/admin/users/{id:guid}/scoped-roles",            GetScopedRoles);

        // ── Memberships ───────────────────────────────────────────────────────
        // UIX-002: assign user to organization, set primary, remove (scaffold)
        routes.MapPost("/api/admin/users/{id:guid}/memberships",                                   AssignMembership);
        routes.MapPost("/api/admin/users/{id:guid}/memberships/{membershipId:guid}/set-primary",   SetPrimaryMembership);
        routes.MapDelete("/api/admin/users/{id:guid}/memberships/{membershipId:guid}",             RemoveMembership);

        // ── Groups ────────────────────────────────────────────────────────────
        // UIX-002: tenant-scoped group management
        routes.MapGet("/api/admin/groups",                              ListGroups);
        routes.MapGet("/api/admin/groups/{id:guid}",                    GetGroup);
        routes.MapPost("/api/admin/groups",                             CreateGroup);
        routes.MapPost("/api/admin/groups/{id:guid}/members",           AddGroupMember);
        routes.MapDelete("/api/admin/groups/{id:guid}/members/{userId:guid}", RemoveGroupMember);

        // ── Permissions catalog ───────────────────────────────────────────────
        // UIX-002: read-only capability/permission catalog
        routes.MapGet("/api/admin/permissions",                         ListPermissions);

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

        // Always return ALL active platform products so the entitlements panel
        // is never empty for newly created tenants. Products not yet in TenantProducts
        // are returned with enabled=false so they can be toggled on.
        var allProducts = await db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var entitlements = allProducts.Select(p =>
        {
            var tp = t.TenantProducts.FirstOrDefault(x => x.ProductId == p.Id);
            // Return the frontend ProductCode ('SynqFund') rather than the raw DB code ('SYNQ_FUND')
            // so the mapper and PRODUCT_META lookup in the Control Center panel work without transforms.
            var frontendCode = DbToFrontendProductCode.TryGetValue(p.Code, out var fc) ? fc : p.Name;
            return new
            {
                productCode  = frontendCode,
                productName  = p.Name,
                enabled      = tp?.IsEnabled ?? false,
                status       = (tp?.IsEnabled ?? false) ? "Active" : "Disabled",
                enabledAtUtc = tp?.EnabledAtUtc,
            };
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

    /// <summary>
    /// POST /api/admin/tenants
    ///
    /// Creates a new tenant and a default admin user in a single atomic transaction.
    /// Returns the new tenant details and a one-time temporary password for the admin user.
    ///
    /// Validations:
    ///   - Tenant code must be unique (case-insensitive).
    ///   - Admin email must not already exist.
    ///   - Code: 2–12 alphanumeric characters (uppercased automatically).
    /// </summary>
    private static async Task<IResult> CreateTenant(
        CreateTenantRequest  body,
        IdentityDbContext    db,
        IPasswordHasher      passwordHasher,
        IAuditEventClient    auditClient,
        CancellationToken    ct)
    {
        // ── Validate inputs ───────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "Tenant name is required." });

        if (string.IsNullOrWhiteSpace(body.Code))
            return Results.BadRequest(new { error = "Tenant code is required." });

        var code = body.Code.ToUpperInvariant().Trim();
        if (code.Length < 2 || code.Length > 12 || !code.All(c => char.IsLetterOrDigit(c)))
            return Results.BadRequest(new { error = "Code must be 2–12 alphanumeric characters." });

        if (string.IsNullOrWhiteSpace(body.AdminEmail))
            return Results.BadRequest(new { error = "Admin email is required." });

        if (string.IsNullOrWhiteSpace(body.AdminFirstName))
            return Results.BadRequest(new { error = "Admin first name is required." });

        if (string.IsNullOrWhiteSpace(body.AdminLastName))
            return Results.BadRequest(new { error = "Admin last name is required." });

        // ── Uniqueness checks ──────────────────────────────────────────────────
        var codeExists = await db.Tenants.AnyAsync(t => t.Code == code, ct);
        if (codeExists)
            return Results.Conflict(new { error = $"A tenant with code '{code}' already exists." });

        var emailNorm = body.AdminEmail.ToLowerInvariant().Trim();
        var emailExists = await db.Users.AnyAsync(u => u.Email == emailNorm, ct);
        if (emailExists)
            return Results.Conflict(new { error = $"A user with email '{emailNorm}' already exists." });

        // ── Resolve organization type ──────────────────────────────────────────
        // GUIDs mirror SeedIds in Identity.Infrastructure (which is internal to that assembly).
        var orgTypeId = body.OrgType switch
        {
            "PROVIDER"   => new Guid("70000000-0000-0000-0000-000000000003"),
            "FUNDER"     => new Guid("70000000-0000-0000-0000-000000000004"),
            "LIEN_OWNER" => new Guid("70000000-0000-0000-0000-000000000005"),
            _            => new Guid("70000000-0000-0000-0000-000000000002"),  // default: LAW_FIRM
        };

        // ── Find TenantAdmin role ──────────────────────────────────────────────
        var tenantAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "TenantAdmin", ct);
        if (tenantAdminRole is null)
            return Results.Problem("TenantAdmin role not found. Contact platform support.", statusCode: 500);

        // ── Generate a one-time temporary password ─────────────────────────────
        var tempPassword = GenerateTemporaryPassword();
        var passwordHash = passwordHasher.Hash(tempPassword);

        // ── Create tenant + org + user + membership + role assignment ──────────
        var tenant = Tenant.Create(body.Name.Trim(), code);
        db.Tenants.Add(tenant);

        // Default organization — same name as the tenant, typed by OrgType.
        var org = Organization.Create(
            tenantId:         tenant.Id,
            name:             body.Name.Trim(),
            organizationTypeId: orgTypeId,
            displayName:      body.Name.Trim());
        db.Organizations.Add(org);

        var user = User.Create(
            tenantId:     tenant.Id,
            email:        emailNorm,
            passwordHash: passwordHash,
            firstName:    body.AdminFirstName.Trim(),
            lastName:     body.AdminLastName.Trim());
        db.Users.Add(user);

        // Add the admin user as an ADMIN member of the default organization.
        var membership = UserOrganizationMembership.Create(
            userId:         user.Id,
            organizationId: org.Id,
            memberRole:     MemberRole.Admin);
        db.UserOrganizationMemberships.Add(membership);

        var sra = ScopedRoleAssignment.Create(
            userId:    user.Id,
            roleId:    tenantAdminRole.Id,
            scopeType: ScopedRoleAssignment.ScopeTypes.Global,
            tenantId:  tenant.Id);
        db.ScopedRoleAssignments.Add(sra);

        await db.SaveChangesAsync(ct);

        // ── Canonical audit event ──────────────────────────────────────────────
        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "platform.admin.tenant.created",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Platform },
            Actor         = new AuditEventActorDto { Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "Tenant", Id = tenant.Id.ToString() },
            Action        = "TenantCreated",
            Description   = $"Tenant '{tenant.Name}' ({tenant.Code}) created with default admin '{emailNorm}'.",
            After         = JsonSerializer.Serialize(new { tenantId = tenant.Id, code = tenant.Code, adminEmail = emailNorm }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "platform.admin.tenant.created", tenant.Id.ToString()),
            Tags = ["tenant-management", "onboarding"],
        });

        return Results.Created(
            $"/api/admin/tenants/{tenant.Id}",
            new
            {
                tenantId          = tenant.Id,
                displayName       = tenant.Name,
                code              = tenant.Code,
                status            = "Active",
                adminUserId       = user.Id,
                adminEmail        = user.Email,
                temporaryPassword = tempPassword,
            });
    }

    /// <summary>
    /// Generates a secure random temporary password: 12 characters,
    /// mixing uppercase, lowercase, digits, and symbols.
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower   = "abcdefghjkmnpqrstuvwxyz";
        const string digits  = "23456789";
        const string symbols = "!@#$%&*";
        var all = upper + lower + digits + symbols;

        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[16];
        rng.GetBytes(bytes);

        var chars = new char[12];
        chars[0]  = upper  [bytes[0]  % upper.Length];
        chars[1]  = lower  [bytes[1]  % lower.Length];
        chars[2]  = digits [bytes[2]  % digits.Length];
        chars[3]  = symbols[bytes[3]  % symbols.Length];
        for (int i = 4; i < 12; i++)
            chars[i] = all[bytes[i] % all.Length];

        // Shuffle
        rng.GetBytes(bytes);
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = bytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
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

    // Maps the frontend ProductCode (TypeScript) → the DB product Code column.
    // Keeps the two representations decoupled without touching the DB schema.
    private static readonly Dictionary<string, string> FrontendToDbProductCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SynqFund"]    = "SYNQ_FUND",
        ["SynqLien"]    = "SYNQ_LIENS",
        ["CareConnect"] = "SYNQ_CARECONNECT",
    };

    // Maps the DB product Code column → the frontend ProductCode (TypeScript).
    private static readonly Dictionary<string, string> DbToFrontendProductCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYNQ_FUND"]        = "SynqFund",
        ["SYNQ_LIENS"]       = "SynqLien",
        ["SYNQ_CARECONNECT"] = "CareConnect",
    };

    private static async Task<IResult> UpdateEntitlement(
        Guid   id,
        string productCode,   // from route: /entitlements/{productCode}
        IdentityDbContext db,
        EntitlementRequest body,
        IAuditEventClient auditClient)
    {
        // Resolve the frontend productCode ('SynqFund') → DB code ('SYNQ_FUND').
        if (!FrontendToDbProductCode.TryGetValue(productCode, out var dbCode))
            dbCode = productCode; // pass-through if already a raw DB code

        var tenant = await db.Tenants
            .Include(t => t.TenantProducts)
                .ThenInclude(tp => tp.Product)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null) return Results.NotFound();

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Code == dbCode);

        if (product is null)
            return Results.NotFound(new { error = $"Product '{productCode}' not found." });

        // ── 1. Update TenantProducts (tenant-level license) ──────────────────
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

        // ── 2. Cascade to OrganizationProducts (org-level, drives product roles) ─
        // Product roles in the JWT are derived from OrganizationProducts, so the
        // tenant-level toggle must propagate to every active org in the tenant.
        var tenantOrgs = await db.Organizations
            .Include(o => o.OrganizationProducts)
            .Where(o => o.TenantId == id && o.IsActive)
            .ToListAsync();

        foreach (var org in tenantOrgs)
        {
            var orgProduct = org.OrganizationProducts
                .FirstOrDefault(op => op.ProductId == product.Id);

            if (orgProduct is null)
            {
                if (body.Enabled)
                    db.OrganizationProducts.Add(
                        OrganizationProduct.Create(org.Id, product.Id));
            }
            else
            {
                if (body.Enabled)
                    orgProduct.Enable();
                else
                    orgProduct.Disable();
            }
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
            Description   = $"Product entitlement '{productCode}' {(body.Enabled ? "enabled" : "disabled")} for tenant {id}.",
            After         = JsonSerializer.Serialize(new { productCode, enabled = body.Enabled }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "platform.admin.tenant.entitlement.updated", id.ToString(), productCode),
            Tags = ["entitlement", "tenant-admin"],
        });

        return Results.Ok(new
        {
            tenantId    = tenant.Id,
            productCode,            // frontend ProductCode (e.g., 'SynqFund')
            enabled     = body.Enabled,
            status      = body.Enabled ? "Active" : "Disabled",
        });
    }

    // =========================================================================
    // USERS
    // =========================================================================

    private static async Task<IResult> ListUsers(
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        int    page     = 1,
        int    pageSize = 20,
        string search   = "",
        string tenantId = "",
        string status   = "")
    {
        var q = db.Users
            .Include(u => u.Tenant)
            .AsQueryable();

        // ── Tenant scoping: TenantAdmin is always restricted to their own tenant ──
        // PlatformAdmin may pass an explicit tenantId filter or see all.
        var callerTenantId = caller.FindFirstValue("tenant_id");
        var isPlatformAdmin = caller.IsInRole("PlatformAdmin");

        if (!isPlatformAdmin && callerTenantId is not null && Guid.TryParse(callerTenantId, out var callerTid))
        {
            // TenantAdmin: always scope to own tenant — ignore any tenantId param
            q = q.Where(u => u.TenantId == callerTid);
        }
        else if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var tid))
        {
            // PlatformAdmin with explicit tenant filter
            q = q.Where(u => u.TenantId == tid);
        }

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u =>
                u.Email.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search));

        // ── Status filter ──────────────────────────────────────────────────────
        var statusNorm = status.ToLowerInvariant().Trim();
        if (statusNorm == "active")
            q = q.Where(u => u.IsActive);
        else if (statusNorm == "inactive")
            q = q.Where(u => !u.IsActive &&
                !db.UserInvitations.Any(i => i.UserId == u.Id && i.Status == UserInvitation.Statuses.Pending));
        else if (statusNorm == "invited")
            q = q.Where(u => !u.IsActive &&
                db.UserInvitations.Any(i => i.UserId == u.Id && i.Status == UserInvitation.Statuses.Pending));

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
                status     = u.IsActive
                    ? "Active"
                    : db.UserInvitations.Any(i => i.UserId == u.Id && i.Status == UserInvitation.Statuses.Pending)
                        ? "Invited"
                        : "Inactive",
                primaryOrg = db.UserOrganizationMemberships
                               .Where(m => m.UserId == u.Id && m.IsPrimary && m.IsActive)
                               .Select(m => m.Organization.DisplayName ?? m.Organization.Name)
                               .FirstOrDefault(),
                groupCount = db.GroupMemberships.Count(gm => gm.UserId == u.Id),
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

    private static async Task<IResult> GetUser(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var isPlatformAdmin = caller.IsInRole("PlatformAdmin");
        var callerTenantId  = caller.FindFirstValue("tenant_id");

        var u = await db.Users
            .Include(u => u.Tenant)
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global))
                .ThenInclude(s => s.Role)
            .Include(u => u.OrganizationMemberships.Where(m => m.IsActive))
                .ThenInclude(m => m.Organization)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (u is null) return Results.NotFound();

        // Non-PlatformAdmins may only view users within their own tenant.
        if (!isPlatformAdmin)
        {
            if (callerTenantId is null || !Guid.TryParse(callerTenantId, out var callerTid) || u.TenantId != callerTid)
                return Results.Forbid();
        }

        var hasPendingInvite = await db.UserInvitations.AnyAsync(
            i => i.UserId == id && i.Status == UserInvitation.Statuses.Pending, ct);

        var groupMemberships = await db.GroupMemberships
            .Include(gm => gm.Group)
            .Where(gm => gm.UserId == id)
            .Select(gm => new { groupId = gm.GroupId, groupName = gm.Group.Name, joinedAtUtc = gm.JoinedAtUtc })
            .ToListAsync(ct);

        var status = u.IsActive ? "Active" : (hasPendingInvite ? "Invited" : "Inactive");

        return Results.Ok(new
        {
            id                = u.Id,
            firstName         = u.FirstName,
            lastName          = u.LastName,
            email             = u.Email,
            role              = u.ScopedRoleAssignments.Select(s => s.Role.Name).FirstOrDefault() ?? "User",
            roles             = u.ScopedRoleAssignments.Select(s => new { roleId = s.RoleId, roleName = s.Role.Name, assignmentId = s.Id }),
            status,
            tenantId          = u.TenantId,
            tenantCode        = u.Tenant.Code,
            tenantDisplayName = u.Tenant.Name,
            createdAtUtc      = u.CreatedAtUtc,
            updatedAtUtc      = u.UpdatedAtUtc,
            isLocked          = false,
            memberships = u.OrganizationMemberships.Select(m => new
            {
                membershipId   = m.Id,
                organizationId = m.OrganizationId,
                orgName        = m.Organization.DisplayName ?? m.Organization.Name,
                memberRole     = m.MemberRole,
                isPrimary      = m.IsPrimary,
                joinedAtUtc    = m.JoinedAtUtc,
            }),
            groups            = groupMemberships,
            groupCount        = groupMemberships.Count,
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

    // =========================================================================
    // UIX-002: USER ACTIVATION
    // =========================================================================

    /// <summary>
    /// POST /api/admin/users/{id}/activate
    /// Sets the user's IsActive flag to true. Idempotent.
    /// Emits identity.user.activated audit event.
    /// </summary>
    private static async Task<IResult> ActivateUser(
        Guid              id,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        var changed = user.Activate();
        if (!changed) return Results.NoContent();

        await db.SaveChangesAsync(ct);

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.activated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = user.TenantId.ToString() },
            Actor = new AuditEventActorDto { Type = ActorType.System, Name = "admin-api" },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "UserActivated",
            Description = $"User '{user.Email}' activated in tenant {user.TenantId}.",
            Before      = JsonSerializer.Serialize(new { isActive = false, email = user.Email }),
            After       = JsonSerializer.Serialize(new { isActive = true,  email = user.Email }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.activated", user.Id.ToString()),
            Tags = ["user-management", "lifecycle", "activation"],
        });

        return Results.NoContent();
    }

    // =========================================================================
    // UIX-002: INVITE USER
    // =========================================================================

    /// <summary>
    /// POST /api/admin/users/invite
    /// Creates a new inactive user record and a pending UserInvitation.
    /// The invitation token is logged to the console in non-production (no email sender yet).
    /// Returns 201 with the new userId and invitationId.
    /// </summary>
    private static async Task<IResult> InviteUser(
        InviteUserRequest body,
        IdentityDbContext db,
        IPasswordHasher   passwordHasher,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Email))
            return Results.BadRequest(new { error = "email is required." });
        if (string.IsNullOrWhiteSpace(body.FirstName))
            return Results.BadRequest(new { error = "firstName is required." });
        if (string.IsNullOrWhiteSpace(body.LastName))
            return Results.BadRequest(new { error = "lastName is required." });
        if (body.TenantId == Guid.Empty)
            return Results.BadRequest(new { error = "tenantId is required." });

        var tenant = await db.Tenants.FindAsync([body.TenantId], ct);
        if (tenant is null) return Results.NotFound(new { error = $"Tenant '{body.TenantId}' not found." });

        var emailLower = body.Email.ToLowerInvariant().Trim();
        var existing = await db.Users.AnyAsync(u => u.TenantId == body.TenantId && u.Email == emailLower, ct);
        if (existing)
            return Results.Conflict(new { error = $"User with email '{emailLower}' already exists in this tenant." });

        // Create user as inactive (not yet accepted invite).
        var tempPasswordHash = passwordHasher.Hash(Guid.NewGuid().ToString());
        var user = User.Create(body.TenantId, emailLower, tempPasswordHash, body.FirstName.Trim(), body.LastName.Trim());
        user.Deactivate();
        db.Users.Add(user);

        // Assign initial role if provided.
        if (body.RoleId.HasValue && body.RoleId.Value != Guid.Empty)
        {
            var role = await db.Roles.FindAsync([body.RoleId.Value], ct);
            if (role is not null)
            {
                var sra = ScopedRoleAssignment.Create(user.Id, role.Id, ScopedRoleAssignment.ScopeTypes.Global, tenantId: body.TenantId, assignedByUserId: body.InvitedByUserId);
                db.ScopedRoleAssignments.Add(sra);
            }
        }

        // Create invitation token (raw token logged; hash stored).
        var rawToken   = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var tokenHash  = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        var invitation = UserInvitation.Create(user.Id, body.TenantId, tokenHash, UserInvitation.PortalOrigins.TenantPortal, body.InvitedByUserId);
        db.UserInvitations.Add(invitation);

        await db.SaveChangesAsync(ct);

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.invited",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = body.TenantId.ToString() },
            Actor = new AuditEventActorDto { Type = ActorType.System, Name = "admin-api" },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "UserInvited",
            Description = $"User '{emailLower}' invited to tenant {body.TenantId}.",
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.invited", invitation.Id.ToString()),
            Tags = ["user-management", "invite"],
        });

        // DEV ONLY: log the raw token so the invite link can be tested without email.
        Console.WriteLine($"[INVITE TOKEN — dev only] userId={user.Id} token={rawToken}");

        return Results.Created(
            $"/api/admin/users/{user.Id}",
            new { userId = user.Id, invitationId = invitation.Id, email = emailLower });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/resend-invite
    /// Revokes all pending invitations for the user, creates a new one.
    /// </summary>
    private static async Task<IResult> ResendInvite(
        Guid              id,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        var pending = await db.UserInvitations
            .Where(i => i.UserId == id && i.Status == UserInvitation.Statuses.Pending)
            .ToListAsync(ct);

        foreach (var inv in pending) inv.Revoke();

        var rawToken  = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        var newInvite = UserInvitation.Create(id, user.TenantId, tokenHash, UserInvitation.PortalOrigins.TenantPortal);
        db.UserInvitations.Add(newInvite);

        await db.SaveChangesAsync(ct);

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.invite_resent",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Scope = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = user.TenantId.ToString() },
            Actor = new AuditEventActorDto { Type = ActorType.System, Name = "admin-api" },
            Entity = new AuditEventEntityDto { Type = "User", Id = id.ToString() },
            Action      = "InviteResent",
            Description = $"Invite resent for user '{user.Email}'.",
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.invite_resent", newInvite.Id.ToString()),
            Tags = ["user-management", "invite"],
        });

        Console.WriteLine($"[RESEND INVITE — dev only] userId={id} token={rawToken}");

        return Results.Ok(new { invitationId = newInvite.Id });
    }

    // =========================================================================
    // UIX-002: MEMBERSHIPS
    // =========================================================================

    /// <summary>
    /// POST /api/admin/users/{id}/memberships
    /// Assigns the user to an organization with the given member role.
    /// </summary>
    private static async Task<IResult> AssignMembership(
        Guid                    id,
        AssignMembershipRequest body,
        IdentityDbContext       db,
        CancellationToken       ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        var org = await db.Organizations.FindAsync([body.OrganizationId], ct);
        if (org is null) return Results.NotFound(new { error = $"Organization '{body.OrganizationId}' not found." });

        if (org.TenantId != user.TenantId)
            return Results.BadRequest(new { error = "Organization does not belong to the user's tenant." });

        var exists = await db.UserOrganizationMemberships.AnyAsync(
            m => m.UserId == id && m.OrganizationId == body.OrganizationId, ct);
        if (exists)
            return Results.Conflict(new { error = "User is already a member of this organization." });

        var memberRole = string.IsNullOrWhiteSpace(body.MemberRole) ? MemberRole.Member : body.MemberRole;
        var membership = UserOrganizationMembership.Create(id, body.OrganizationId, memberRole, body.GrantedByUserId);

        // If this is the first/only membership, make it primary automatically.
        var hasPrimary = await db.UserOrganizationMemberships.AnyAsync(m => m.UserId == id && m.IsPrimary, ct);
        if (!hasPrimary) membership.SetPrimary();

        db.UserOrganizationMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/admin/users/{id}/memberships/{membership.Id}",
            new
            {
                membershipId   = membership.Id,
                userId         = id,
                organizationId = body.OrganizationId,
                memberRole     = membership.MemberRole,
                isPrimary      = membership.IsPrimary,
            });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/memberships/{membershipId}/set-primary
    /// Makes the specified membership the primary org for the user.
    /// Clears the primary flag on any other memberships.
    /// </summary>
    private static async Task<IResult> SetPrimaryMembership(
        Guid              id,
        Guid              membershipId,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var target = await db.UserOrganizationMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.UserId == id, ct);
        if (target is null)
            return Results.NotFound(new { error = $"Membership '{membershipId}' not found for user '{id}'." });

        var others = await db.UserOrganizationMemberships
            .Where(m => m.UserId == id && m.Id != membershipId && m.IsPrimary)
            .ToListAsync(ct);

        foreach (var o in others) o.ClearPrimary();
        target.SetPrimary();

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// DELETE /api/admin/users/{id}/memberships/{membershipId}
    /// Deactivates the membership. Enforces membership safety rules:
    ///   - 409 if this is the user's last active membership.
    ///   - 409 if this is the primary membership and other memberships still exist
    ///     (caller must designate a new primary first via set-primary).
    /// </summary>
    private static async Task<IResult> RemoveMembership(
        Guid              id,
        Guid              membershipId,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var membership = await db.UserOrganizationMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.UserId == id && m.IsActive, ct);
        if (membership is null)
            return Results.NotFound(new { error = $"Membership '{membershipId}' not found for user '{id}'." });

        // ── Safety rule 1: cannot remove the last active membership ───────────
        var activeMembershipCount = await db.UserOrganizationMemberships
            .CountAsync(m => m.UserId == id && m.IsActive, ct);

        if (activeMembershipCount <= 1)
            return Results.Conflict(new
            {
                error = "Cannot remove the user's last remaining organization membership. " +
                        "Assign the user to another organization first.",
                code  = "LAST_MEMBERSHIP",
            });

        // ── Safety rule 2: cannot remove primary membership while others exist ─
        // Caller must designate another primary first via set-primary.
        if (membership.IsPrimary)
            return Results.Conflict(new
            {
                error = "Cannot remove the primary membership. " +
                        "Please designate another membership as primary first.",
                code  = "PRIMARY_MEMBERSHIP",
            });

        membership.Deactivate();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // =========================================================================
    // UIX-002: GROUPS
    // =========================================================================

    /// <summary>
    /// GET /api/admin/groups
    /// Returns the list of tenant-scoped groups. Optionally filtered by tenantId.
    /// </summary>
    private static async Task<IResult> ListGroups(
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        string tenantId = "",
        int    page     = 1,
        int    pageSize = 50,
        CancellationToken ct = default)
    {
        var q = db.TenantGroups
            .Include(g => g.Members)
            .AsQueryable();

        // ── Tenant scoping: TenantAdmin always restricted to own tenant ────────
        var callerTenantId = caller.FindFirstValue("tenant_id");
        var isPlatformAdmin = caller.IsInRole("PlatformAdmin");

        if (!isPlatformAdmin && callerTenantId is not null && Guid.TryParse(callerTenantId, out var callerTid))
        {
            q = q.Where(g => g.TenantId == callerTid);
        }
        else if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var tid))
        {
            q = q.Where(g => g.TenantId == tid);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new
            {
                id          = g.Id,
                tenantId    = g.TenantId,
                name        = g.Name,
                description = g.Description,
                memberCount = g.Members.Count,
                isActive    = g.IsActive,
                createdAtUtc = g.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount = total, page, pageSize });
    }

    /// <summary>
    /// GET /api/admin/groups/{id}
    /// Returns a group with its full member list.
    /// </summary>
    private static async Task<IResult> GetGroup(
        Guid              id,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var group = await db.TenantGroups
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (group is null) return Results.NotFound();

        return Results.Ok(new
        {
            id          = group.Id,
            tenantId    = group.TenantId,
            name        = group.Name,
            description = group.Description,
            isActive    = group.IsActive,
            createdAtUtc = group.CreatedAtUtc,
            updatedAtUtc = group.UpdatedAtUtc,
            members = group.Members.Select(m => new
            {
                membershipId = m.Id,
                userId       = m.UserId,
                firstName    = m.User.FirstName,
                lastName     = m.User.LastName,
                email        = m.User.Email,
                joinedAtUtc  = m.JoinedAtUtc,
            }),
            memberCount = group.Members.Count,
        });
    }

    /// <summary>
    /// POST /api/admin/groups
    /// Creates a new tenant-scoped group.
    /// </summary>
    private static async Task<IResult> CreateGroup(
        CreateGroupRequest body,
        IdentityDbContext  db,
        ClaimsPrincipal    caller,
        CancellationToken  ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "name is required." });
        if (body.TenantId == Guid.Empty)
            return Results.BadRequest(new { error = "tenantId is required." });

        // ── Tenant boundary: TenantAdmin may only create groups in their own tenant ──
        var callerTenantId = caller.FindFirstValue("tenant_id");
        var isPlatformAdmin = caller.IsInRole("PlatformAdmin");

        if (!isPlatformAdmin && callerTenantId is not null &&
            Guid.TryParse(callerTenantId, out var callerTid) &&
            body.TenantId != callerTid)
        {
            return Results.Forbid();
        }

        var tenant = await db.Tenants.FindAsync([body.TenantId], ct);
        if (tenant is null) return Results.NotFound(new { error = $"Tenant '{body.TenantId}' not found." });

        var nameExists = await db.TenantGroups.AnyAsync(
            g => g.TenantId == body.TenantId && g.Name == body.Name.Trim(), ct);
        if (nameExists)
            return Results.Conflict(new { error = $"A group named '{body.Name}' already exists in this tenant." });

        var group = TenantGroup.Create(body.TenantId, body.Name, body.Description, body.CreatedByUserId);
        db.TenantGroups.Add(group);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/admin/groups/{group.Id}",
            new { id = group.Id, tenantId = group.TenantId, name = group.Name });
    }

    /// <summary>
    /// POST /api/admin/groups/{id}/members
    /// Adds a user to a group. Idempotent — ignores if already a member.
    /// </summary>
    private static async Task<IResult> AddGroupMember(
        Guid                   id,
        AddGroupMemberRequest  body,
        IdentityDbContext      db,
        CancellationToken      ct)
    {
        var group = await db.TenantGroups.FindAsync([id], ct);
        if (group is null) return Results.NotFound(new { error = $"Group '{id}' not found." });

        var user = await db.Users.FindAsync([body.UserId], ct);
        if (user is null) return Results.NotFound(new { error = $"User '{body.UserId}' not found." });

        if (user.TenantId != group.TenantId)
            return Results.BadRequest(new { error = "User does not belong to the group's tenant." });

        var alreadyMember = await db.GroupMemberships.AnyAsync(
            m => m.GroupId == id && m.UserId == body.UserId, ct);
        if (alreadyMember)
            return Results.Ok(new { message = "User is already a member of this group." });

        var membership = GroupMembership.Create(id, body.UserId, group.TenantId, body.AddedByUserId);
        db.GroupMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/admin/groups/{id}/members/{body.UserId}",
            new { groupId = id, userId = body.UserId, joinedAtUtc = membership.JoinedAtUtc });
    }

    /// <summary>
    /// DELETE /api/admin/groups/{id}/members/{userId}
    /// Removes a user from a group.
    /// </summary>
    private static async Task<IResult> RemoveGroupMember(
        Guid              id,
        Guid              userId,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var membership = await db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == userId, ct);
        if (membership is null)
            return Results.NotFound(new { error = $"User '{userId}' is not a member of group '{id}'." });

        db.GroupMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // =========================================================================
    // UIX-002: PERMISSIONS CATALOG
    // =========================================================================

    /// <summary>
    /// GET /api/admin/permissions
    /// Returns the full capabilities catalog — all active product-scoped capabilities
    /// that represent the platform permission surface.
    /// </summary>
    private static async Task<IResult> ListPermissions(
        IdentityDbContext db,
        string productId = "",
        CancellationToken ct = default)
    {
        var q = db.Capabilities
            .Include(c => c.Product)
            .Where(c => c.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(productId) && Guid.TryParse(productId, out var pid))
            q = q.Where(c => c.ProductId == pid);

        var items = await q
            .OrderBy(c => c.Product.Name)
            .ThenBy(c => c.Code)
            .Select(c => new
            {
                id          = c.Id,
                code        = c.Code,
                name        = c.Name,
                description = c.Description,
                productId   = c.ProductId,
                productName = c.Product.Name,
                isActive    = c.IsActive,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount = items.Count });
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
    private record CreateTenantRequest(
        string  Name,
        string  Code,
        string  AdminEmail,
        string  AdminFirstName,
        string  AdminLastName,
        string? OrgType = null);  // LAW_FIRM | PROVIDER | FUNDER | LIEN_OWNER (defaults to LAW_FIRM)
    private record EntitlementRequest(bool Enabled);
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

    // UIX-002 request DTOs
    private record InviteUserRequest(
        Guid    TenantId,
        string  Email,
        string  FirstName,
        string  LastName,
        Guid?   RoleId          = null,
        Guid?   InvitedByUserId = null);

    private record AssignMembershipRequest(
        Guid    OrganizationId,
        string? MemberRole      = null,
        Guid?   GrantedByUserId = null);

    private record CreateGroupRequest(
        Guid    TenantId,
        string  Name,
        string? Description     = null,
        Guid?   CreatedByUserId = null);

    private record AddGroupMemberRequest(
        Guid  UserId,
        Guid? AddedByUserId = null);

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
