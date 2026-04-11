using System.Security.Claims;
using System.Text.Json;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Identity.Domain;
using Identity.Infrastructure.Data;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        // All admin routes require a valid, authenticated JWT.
        // Defense-in-depth: enforced here at the service level independent of the gateway.
        var adminGroup = routes.MapGroup("/api/admin").RequireAuthorization();

        // ── Tenants ──────────────────────────────────────────────────────────
        adminGroup.MapGet("/tenants",           ListTenants);
        adminGroup.MapPost("/tenants",          CreateTenant);
        adminGroup.MapGet("/tenants/{id:guid}", GetTenant);
        adminGroup.MapPost("/tenants/{id:guid}/entitlements/{productCode}", UpdateEntitlement);
        adminGroup.MapPatch("/tenants/{id:guid}/session-settings", UpdateTenantSessionSettings);
        adminGroup.MapPatch("/tenants/{id:guid}/logo",             SetTenantLogo);
        adminGroup.MapDelete("/tenants/{id:guid}/logo",            ClearTenantLogo);
        adminGroup.MapPost("/tenants/{id:guid}/provisioning/retry", RetryProvisioning);
        adminGroup.MapPost("/tenants/{id:guid}/verification/retry", RetryVerification);

        // ── Infrastructure DNS ──────────────────────────────────────────
        adminGroup.MapPost("/dns/provision", ProvisionInfraSubdomain);

        // ── Users ─────────────────────────────────────────────────────────
        adminGroup.MapGet("/users",           ListUsers);
        adminGroup.MapGet("/users/{id:guid}", GetUser);

        // ── Roles ──────────────────────────────────────────────────────────
        adminGroup.MapGet("/roles",           ListRoles);
        adminGroup.MapGet("/roles/{id:guid}", GetRole);

        // ── Audit Logs ────────────────────────────────────────────────────
        adminGroup.MapGet("/audit",           ListAudit);

        // ── Platform Settings (static seed — no DB table yet) ─────────────
        adminGroup.MapGet("/settings",            ListSettings);
        adminGroup.MapPut("/settings/{key}",      UpdateSetting);

        // ── Support Cases (not yet persisted — empty stubs) ───────────────
        adminGroup.MapGet("/support",             ListSupport);
        adminGroup.MapGet("/support/{id}",        GetSupport);
        adminGroup.MapPost("/support",            CreateSupport);
        adminGroup.MapPost("/support/{id}/notes", AddSupportNote);
        adminGroup.MapPatch("/support/{id}/status", UpdateSupportStatus);

        // ── LSCC-010: Provider auto-provisioning — minimal org creation ──────
        // Internal service-to-service endpoint.  Token-gated at the gateway.
        // Creates a minimal PROVIDER Organization for a CareConnect provider.
        // Idempotent: returns the existing org if already provisioned.
        adminGroup.MapGet("/organizations",           ListOrganizations);
        adminGroup.MapPost("/organizations",          AdminEndpointsLscc010.CreateProviderOrganization);
        adminGroup.MapGet("/organizations/{id:guid}", AdminEndpointsLscc010.GetOrganizationById);
        adminGroup.MapPut("/organizations/{id:guid}", UpdateOrganization);

        // ── Platform Foundation — Phase 1-6 ──────────────────────────────
        adminGroup.MapGet("/organization-types",             ListOrganizationTypes);
        adminGroup.MapGet("/organization-types/{id:guid}",   GetOrganizationType);

        adminGroup.MapGet("/relationship-types",             ListRelationshipTypes);
        adminGroup.MapGet("/relationship-types/{id:guid}",   GetRelationshipType);

        adminGroup.MapGet("/organization-relationships",     ListOrganizationRelationships);
        adminGroup.MapGet("/organization-relationships/{id:guid}", GetOrganizationRelationship);
        adminGroup.MapPost("/organization-relationships",    CreateOrganizationRelationship);
        adminGroup.MapDelete("/organization-relationships/{id:guid}", DeactivateOrganizationRelationship);

        adminGroup.MapGet("/product-org-type-rules",          ListProductOrgTypeRules);
        // Two URL variants served by the same handler — client uses the short form.
        adminGroup.MapGet("/product-relationship-type-rules", ListProductRelationshipTypeRules);
        adminGroup.MapGet("/product-rel-type-rules",          ListProductRelationshipTypeRules);

        // ── Legacy coverage (Phase G) ────────────────────────────────────────
        adminGroup.MapGet("/legacy-coverage", GetLegacyCoverage);

        // ── Platform readiness summary (Phase 8) ─────────────────────────────
        adminGroup.MapGet("/platform-readiness", GetPlatformReadiness);

        // ── User lifecycle ────────────────────────────────────────────────────
        // Step 27 (Phase B): user deactivation — emits identity.user.deactivated.
        adminGroup.MapPatch("/users/{id:guid}/deactivate",            DeactivateUser);

        // UIX-002: activate user
        adminGroup.MapPost("/users/{id:guid}/activate",               ActivateUser);

        // UIX-002: invite user
        adminGroup.MapPost("/users/invite",                           InviteUser);

        // UIX-002: resend invite
        adminGroup.MapPost("/users/{id:guid}/resend-invite",          ResendInvite);

        // UIX-003-03: security / session admin actions
        adminGroup.MapPost("/users/{id:guid}/lock",                   LockUser);
        adminGroup.MapPost("/users/{id:guid}/unlock",                 UnlockUser);
        adminGroup.MapPost("/users/{id:guid}/reset-password",         AdminResetPassword);
        adminGroup.MapPost("/users/{id:guid}/set-password",           AdminSetPassword);
        adminGroup.MapPost("/users/{id:guid}/force-logout",           ForceLogout);
        adminGroup.MapGet("/users/{id:guid}/security",                GetUserSecurity);

        // UIX-004: user activity audit trail (queries local AuditLogs by EntityId)
        adminGroup.MapGet("/users/{id:guid}/activity",                GetUserActivity);

        // LSCC-01-003: Admin CareConnect provider provisioning
        adminGroup.MapGet("/users/{id:guid}/careconnect-readiness",   GetCareConnectReadiness);
        adminGroup.MapPost("/users/{id:guid}/provision-careconnect",  ProvisionForCareConnect);

        // ── Role assignment ───────────────────────────────────────────────────
        adminGroup.MapPost("/users/{id:guid}/roles",                  AssignRole);
        adminGroup.MapDelete("/users/{id:guid}/roles/{roleId:guid}",  RevokeRole);

        // UIX-002-C: assignable roles with eligibility metadata
        adminGroup.MapGet("/users/{id:guid}/assignable-roles",        GetAssignableRoles);

        // Phase I: scoped role summary for a user (non-global scope visibility)
        adminGroup.MapGet("/users/{id:guid}/scoped-roles",            GetScopedRoles);

        // ── Memberships ───────────────────────────────────────────────────────
        // UIX-002: assign user to organization, set primary, remove (scaffold)
        adminGroup.MapPost("/users/{id:guid}/memberships",                                   AssignMembership);
        adminGroup.MapPost("/users/{id:guid}/memberships/{membershipId:guid}/set-primary",   SetPrimaryMembership);
        adminGroup.MapDelete("/users/{id:guid}/memberships/{membershipId:guid}",             RemoveMembership);

        // ── Groups ────────────────────────────────────────────────────────────
        // UIX-002: tenant-scoped group management
        adminGroup.MapGet("/groups",                              ListGroups);
        adminGroup.MapGet("/groups/{id:guid}",                    GetGroup);
        adminGroup.MapPost("/groups",                             CreateGroup);
        adminGroup.MapPost("/groups/{id:guid}/members",           AddGroupMember);
        adminGroup.MapDelete("/groups/{id:guid}/members/{userId:guid}", RemoveGroupMember);

        // ── Permissions catalog ───────────────────────────────────────────────
        // UIX-002: read-only capability/permission catalog
        adminGroup.MapGet("/permissions",                         ListPermissions);

        // ── Role permission management (UIX-005) ──────────────────────────────
        adminGroup.MapGet("/roles/{id:guid}/permissions",                              GetRolePermissions);
        adminGroup.MapPost("/roles/{id:guid}/permissions",                             AssignRolePermission);
        adminGroup.MapDelete("/roles/{id:guid}/permissions/{capabilityId:guid}",       RevokeRolePermission);

        // ── User effective permissions (UIX-005) ─────────────────────────────
        adminGroup.MapGet("/users/{id:guid}/permissions",                              GetUserEffectivePermissions);

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
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

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
                type               = t.Organizations.OrderBy(o => o.CreatedAtUtc).Select(o => o.OrgType).FirstOrDefault() ?? "LAW_FIRM",
                status             = t.IsActive ? "Active" : "Inactive",
                primaryContactName = t.Users.OrderBy(u => u.CreatedAtUtc).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? "",
                isActive           = t.IsActive,
                userCount          = t.Users.Count,
                orgCount           = t.Organizations.Count,
                createdAtUtc       = t.CreatedAtUtc,
                subdomain          = t.Subdomain,
                provisioningStatus = t.ProvisioningStatus.ToString(),
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

    private static async Task<IResult> GetTenant(Guid id, IdentityDbContext db, IDnsService dnsService)
    {
        var dnsBaseDomain = dnsService.BaseDomain;
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

        var defaultOrg = t.Organizations.OrderBy(o => o.CreatedAtUtc).FirstOrDefault();
        return Results.Ok(new
        {
            id                    = t.Id,
            code                  = t.Code,
            displayName           = t.Name,
            type                  = defaultOrg?.OrgType ?? "LAW_FIRM",
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
            logoDocumentId        = t.LogoDocumentId,
            productEntitlements   = entitlements,
            subdomain                       = t.Subdomain,
            provisioningStatus              = t.ProvisioningStatus.ToString(),
            lastProvisioningAttemptUtc       = t.LastProvisioningAttemptUtc,
            provisioningFailureReason       = t.ProvisioningFailureReason,
            provisioningFailureStage        = t.ProvisioningFailureStage.ToString(),
            hostname                        = t.Subdomain != null
                ? $"{t.Subdomain}.{dnsBaseDomain}"
                : (string?)null,
            verificationAttemptCount        = t.VerificationAttemptCount,
            lastVerificationAttemptUtc      = t.LastVerificationAttemptUtc,
            nextVerificationRetryAtUtc      = t.NextVerificationRetryAtUtc,
            isVerificationRetryExhausted    = t.IsVerificationRetryExhausted,
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
        CreateTenantRequest       body,
        IdentityDbContext         db,
        IPasswordHasher           passwordHasher,
        IAuditEventClient         auditClient,
        ITenantProvisioningService provisioningService,
        IProductProvisioningService productProvisioningEngine,
        ILoggerFactory            loggerFactory,
        CancellationToken         ct)
    {
        // ── Validate inputs ───────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "Tenant name is required." });

        if (string.IsNullOrWhiteSpace(body.Code))
            return Results.BadRequest(new { error = "Tenant code is required." });

        var code = body.Code.ToUpperInvariant().Trim();
        if (code.Length < 2 || code.Length > 12 || !code.All(c => char.IsLetterOrDigit(c)))
            return Results.BadRequest(new { error = "Code must be 2–12 alphanumeric characters." });

        if (!string.IsNullOrWhiteSpace(body.PreferredSubdomain))
        {
            var (slugValid, slugError) = SlugGenerator.Validate(SlugGenerator.Normalize(body.PreferredSubdomain));
            if (!slugValid)
                return Results.BadRequest(new { error = slugError });
        }

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
        var orgTypeId = body.OrgType switch
        {
            "PROVIDER"   => new Guid("70000000-0000-0000-0000-000000000003"),
            "FUNDER"     => new Guid("70000000-0000-0000-0000-000000000004"),
            "LIEN_OWNER" => new Guid("70000000-0000-0000-0000-000000000005"),
            _            => new Guid("70000000-0000-0000-0000-000000000002"),
        };

        // ── Find TenantAdmin role ──────────────────────────────────────────────
        var tenantAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "TenantAdmin", ct);
        if (tenantAdminRole is null)
            return Results.Problem("TenantAdmin role not found. Contact platform support.", statusCode: 500);

        // ── Generate a one-time temporary password ─────────────────────────────
        var tempPassword = GenerateTemporaryPassword();
        var passwordHash = passwordHasher.Hash(tempPassword);

        // ── Create tenant + org + user + membership + role assignment ──────────
        var tenant = Tenant.Create(body.Name.Trim(), code, body.PreferredSubdomain);
        db.Tenants.Add(tenant);

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

        // ── Provision subdomain (DNS + TenantDomain record) ──────────────────
        var log = loggerFactory.CreateLogger("Identity.Api.AdminEndpoints");
        var provResult = await provisioningService.ProvisionAsync(tenant, ct);

        if (provResult.Success)
        {
            log.LogInformation("Subdomain provisioned for tenant {TenantCode}: {Hostname}",
                tenant.Code, provResult.Hostname);
        }
        else
        {
            log.LogWarning("Subdomain provisioning failed for tenant {TenantCode}: {Reason}",
                tenant.Code, provResult.ErrorMessage);
        }

        // ── Product provisioning (if products specified) ────────────────────────
        var productResults = new List<ProvisionProductResult>();
        if (body.Products is { Count: > 0 })
        {
            foreach (var rawCode in body.Products)
            {
                var dbCode = FrontendToDbProductCode.TryGetValue(rawCode, out var mapped)
                    ? mapped : rawCode;
                try
                {
                    var pr = await productProvisioningEngine.ProvisionAsync(
                        new ProvisionProductRequest(tenant.Id, dbCode, true), ct);
                    productResults.Add(pr);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Product provisioning for {ProductCode} failed during tenant onboarding", dbCode);
                }
            }
        }

        // ── Audit events ────────────────────────────────────────────────────────
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
            After         = JsonSerializer.Serialize(new { tenantId = tenant.Id, code = tenant.Code, adminEmail = emailNorm, subdomain = tenant.Subdomain }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "platform.admin.tenant.created", tenant.Id.ToString()),
            Tags = ["tenant-management", "onboarding"],
        });

        if (provResult.Success)
        {
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "platform.admin.tenant.provisioning.succeeded",
                EventCategory = EventCategory.Administrative,
                SourceSystem  = "identity-service",
                SourceService = "admin-api",
                Visibility    = VisibilityScope.Platform,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Platform },
                Actor         = new AuditEventActorDto { Type = ActorType.System },
                Entity        = new AuditEventEntityDto { Type = "Tenant", Id = tenant.Id.ToString() },
                Action        = "ProvisioningSucceeded",
                Description   = $"Subdomain '{tenant.Subdomain}' provisioned for tenant '{tenant.Code}'.",
                After         = JsonSerializer.Serialize(new { hostname = provResult.Hostname, subdomain = tenant.Subdomain }),
                IdempotencyKey = IdempotencyKey.For("identity-service", "platform.admin.tenant.provisioning.succeeded", tenant.Id.ToString()),
                Tags = ["tenant-management", "provisioning"],
            });
        }
        else
        {
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "platform.admin.tenant.provisioning.failed",
                EventCategory = EventCategory.Administrative,
                SourceSystem  = "identity-service",
                SourceService = "admin-api",
                Visibility    = VisibilityScope.Platform,
                Severity      = SeverityLevel.Warn,
                OccurredAtUtc = now,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Platform },
                Actor         = new AuditEventActorDto { Type = ActorType.System },
                Entity        = new AuditEventEntityDto { Type = "Tenant", Id = tenant.Id.ToString() },
                Action        = "ProvisioningFailed",
                Description   = $"Subdomain provisioning failed for tenant '{tenant.Code}': {provResult.ErrorMessage}",
                After         = JsonSerializer.Serialize(new { subdomain = tenant.Subdomain, error = provResult.ErrorMessage }),
                IdempotencyKey = IdempotencyKey.For("identity-service", "platform.admin.tenant.provisioning.failed", tenant.Id.ToString()),
                Tags = ["tenant-management", "provisioning"],
            });
        }

        return Results.Created(
            $"/api/admin/tenants/{tenant.Id}",
            new
            {
                tenantId            = tenant.Id,
                displayName         = tenant.Name,
                code                = tenant.Code,
                status              = "Active",
                adminUserId         = user.Id,
                adminEmail          = user.Email,
                temporaryPassword   = tempPassword,
                subdomain           = tenant.Subdomain,
                provisioningStatus  = tenant.ProvisioningStatus.ToString(),
                hostname            = provResult.Hostname,
                productsProvisioned = productResults.Select(p => new
                {
                    productCode = p.ProductCode,
                    enabled     = p.Enabled,
                    orgProductsCreated = p.OrganizationProductsCreated,
                }).ToList(),
            });
    }

    private static async Task<IResult> RetryProvisioning(
        Guid                       id,
        IdentityDbContext          db,
        ITenantProvisioningService provisioningService,
        IAuditEventClient          auditClient,
        ILoggerFactory             loggerFactory,
        CancellationToken          ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null)
            return Results.NotFound(new { error = "Tenant not found." });

        if (tenant.ProvisioningStatus == ProvisioningStatus.Active)
            return Results.Ok(new { success = true, provisioningStatus = "Active", hostname = (string?)null, error = (string?)null });

        if (tenant.ProvisioningStatus == ProvisioningStatus.InProgress)
            return Results.Conflict(new { error = "Provisioning is already in progress." });

        var log = loggerFactory.CreateLogger("Identity.Api.AdminEndpoints");
        log.LogInformation("Provisioning retry requested for tenant {TenantCode}", tenant.Code);

        var result = await provisioningService.RetryProvisioningAsync(tenant, ct);

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = result.Success
                ? "platform.admin.tenant.provisioning.retry.succeeded"
                : "platform.admin.tenant.provisioning.retry.failed",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = result.Success ? SeverityLevel.Info : SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Platform },
            Actor         = new AuditEventActorDto { Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "Tenant", Id = tenant.Id.ToString() },
            Action        = result.Success ? "ProvisioningRetrySucceeded" : "ProvisioningRetryFailed",
            Description   = result.Success
                ? $"Provisioning retry succeeded for tenant '{tenant.Code}': {result.Hostname}"
                : $"Provisioning retry failed for tenant '{tenant.Code}': {result.ErrorMessage}",
            After         = JsonSerializer.Serialize(new { hostname = result.Hostname, error = result.ErrorMessage }),
            IdempotencyKey = IdempotencyKey.For("identity-service", result.Success ? "provisioning.retry.succeeded" : "provisioning.retry.failed", $"{tenant.Id}:{now.Ticks}"),
            Tags = ["tenant-management", "provisioning", "retry"],
        });

        return Results.Ok(new
        {
            success            = result.Success,
            provisioningStatus = tenant.ProvisioningStatus.ToString(),
            hostname           = result.Hostname,
            error              = result.ErrorMessage,
        });
    }

    private static async Task<IResult> RetryVerification(
        Guid                       id,
        IdentityDbContext          db,
        IVerificationRetryService  retryService,
        IDnsService                dnsService,
        IAuditEventClient          auditClient,
        ILoggerFactory             loggerFactory,
        CancellationToken          ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null)
            return Results.NotFound(new { error = "Tenant not found." });

        if (tenant.ProvisioningStatus == ProvisioningStatus.Active)
            return Results.Ok(new { success = true, provisioningStatus = "Active", hostname = (string?)null, error = (string?)null });

        if (tenant.Subdomain is null)
            return Results.BadRequest(new { error = "Tenant has no subdomain assigned. Run provisioning first." });

        if (tenant.ProvisioningStatus == ProvisioningStatus.InProgress)
            return Results.Conflict(new { error = "Provisioning is already in progress." });

        var log = loggerFactory.CreateLogger("Identity.Api.AdminEndpoints");
        log.LogInformation("Verification retry requested (admin) for tenant {TenantCode}", tenant.Code);

        var hostname = $"{tenant.Subdomain}.{dnsService.BaseDomain}";

        tenant.ResetVerificationRetryState();
        await db.SaveChangesAsync(ct);

        var outcome = await retryService.ExecuteRetryAsync(tenant, hostname, ct);

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = outcome.Succeeded
                ? "platform.admin.tenant.verification.retry.succeeded"
                : "platform.admin.tenant.verification.retry.failed",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = outcome.Succeeded ? SeverityLevel.Info : SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Platform },
            Actor         = new AuditEventActorDto { Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "Tenant", Id = tenant.Id.ToString() },
            Action        = outcome.Succeeded ? "VerificationRetrySucceeded" : "VerificationRetryFailed",
            Description   = outcome.Succeeded
                ? $"Verification retry succeeded for tenant '{tenant.Code}': {hostname}"
                : $"Verification retry failed for tenant '{tenant.Code}' (attempt {outcome.AttemptNumber}): {outcome.LastFailureReason}",
            After         = JsonSerializer.Serialize(new
            {
                hostname,
                error = outcome.LastFailureReason,
                stage = outcome.LastFailureStage.ToString(),
                attempt = outcome.AttemptNumber,
                stillRetrying = outcome.StillRetrying,
                exhausted = outcome.Exhausted,
            }),
            IdempotencyKey = IdempotencyKey.For("identity-service",
                outcome.Succeeded ? "verification.retry.succeeded" : "verification.retry.failed",
                $"{tenant.Id}:{now.Ticks}"),
            Tags = ["tenant-management", "verification", "retry"],
        });

        if (outcome.Succeeded)
            log.LogInformation("Verification retry succeeded for tenant {TenantCode}: {Hostname}", tenant.Code, hostname);

        return Results.Ok(new
        {
            success            = outcome.Succeeded,
            provisioningStatus = tenant.ProvisioningStatus.ToString(),
            hostname           = hostname,
            error              = outcome.LastFailureReason,
            failureStage       = outcome.LastFailureStage.ToString(),
            attemptNumber      = outcome.AttemptNumber,
            stillRetrying      = outcome.StillRetrying,
            exhausted          = outcome.Exhausted,
            nextRetryAtUtc     = outcome.NextRetryAtUtc,
        });
    }

    private static async Task<IResult> ProvisionInfraSubdomain(
        InfraSubdomainRequest    body,
        IDnsService              dns,
        IAuditEventClient        auditClient,
        ILoggerFactory           loggerFactory,
        CancellationToken        ct)
    {
        if (string.IsNullOrWhiteSpace(body.Subdomain))
            return Results.BadRequest(new { error = "Subdomain is required." });

        var slug = body.Subdomain.Trim().ToLowerInvariant();
        var log = loggerFactory.CreateLogger("Identity.Api.AdminEndpoints");

        log.LogInformation("Infrastructure DNS provisioning requested for subdomain {Slug}", slug);

        var success = await dns.CreateSubdomainAsync(slug, ct);
        var hostname = $"{slug}.{dns.BaseDomain}";

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = success ? "platform.admin.infra.dns.created" : "platform.admin.infra.dns.failed",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = success ? SeverityLevel.Info : SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Platform },
            Actor         = new AuditEventActorDto { Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "InfraSubdomain", Id = slug },
            Action        = success ? "InfraDnsCreated" : "InfraDnsFailed",
            Description   = success
                ? $"Infrastructure subdomain '{hostname}' provisioned successfully."
                : $"Infrastructure subdomain '{hostname}' provisioning failed.",
            IdempotencyKey = IdempotencyKey.For("identity-service", "infra.dns", $"{slug}:{now.Ticks}"),
            Tags = ["infrastructure", "dns"],
        });

        return success
            ? Results.Ok(new { success = true, hostname, subdomain = slug })
            : Results.Problem($"DNS provisioning failed for '{hostname}'. Check Route53 configuration.", statusCode: 502);
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

        // Use rejection sampling to avoid modulo bias.
        static char PickUnbiased(string charset, byte[] pool, ref int pos)
        {
            int limit = 256 - (256 % charset.Length);
            byte b;
            do
            {
                if (pos >= pool.Length)
                {
                    System.Security.Cryptography.RandomNumberGenerator.Fill(pool);
                    pos = 0;
                }
                b = pool[pos++];
            } while (b >= limit);
            return charset[b % charset.Length];
        }

        var pool = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(pool);
        int cursor = 0;

        var chars = new char[12];
        // Guarantee at least one character from each required class.
        chars[0] = PickUnbiased(upper,   pool, ref cursor);
        chars[1] = PickUnbiased(lower,   pool, ref cursor);
        chars[2] = PickUnbiased(digits,  pool, ref cursor);
        chars[3] = PickUnbiased(symbols, pool, ref cursor);
        for (int i = 4; i < 12; i++)
            chars[i] = PickUnbiased(all, pool, ref cursor);

        // Fisher-Yates shuffle with unbiased index selection.
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int limit = 256 - (256 % (i + 1));
            byte b;
            do
            {
                if (cursor >= pool.Length) { System.Security.Cryptography.RandomNumberGenerator.Fill(pool); cursor = 0; }
                b = pool[cursor++];
            } while (b >= limit);
            int swapIdx = b % (i + 1);
            (chars[i], chars[swapIdx]) = (chars[swapIdx], chars[i]);
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

    // ── Tenant logo ───────────────────────────────────────────────────────────

    private record SetLogoRequest(string? DocumentId);

    /// <summary>
    /// PATCH /api/admin/tenants/{id}/logo
    /// Sets the tenant's logo by storing the document ID of an already-uploaded image.
    /// The caller is responsible for uploading the image to the Documents service first.
    /// </summary>
    private static async Task<IResult> SetTenantLogo(
        Guid                id,
        SetLogoRequest      body,
        ClaimsPrincipal     caller,
        IdentityDbContext   db,
        IAuditEventClient   auditClient,
        CancellationToken   ct)
    {
        if (string.IsNullOrWhiteSpace(body.DocumentId))
            return Results.BadRequest(new { error = "documentId is required." });

        if (!Guid.TryParse(body.DocumentId, out var documentId))
            return Results.BadRequest(new { error = "documentId must be a valid UUID." });

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return Results.NotFound();

        tenant.SetLogo(documentId);
        await db.SaveChangesAsync(ct);

        var callerId     = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var callerEmail  = caller.FindFirstValue(ClaimTypes.Email) ?? caller.FindFirstValue("email");
        var callerTenant = caller.FindFirstValue("tenant_id");
        var now          = DateTimeOffset.UtcNow;

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.tenant.logo_set",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = id.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = callerId,
                Type = ActorType.User,
                Name = callerEmail,
            },
            Entity      = new AuditEventEntityDto { Type = "Tenant", Id = id.ToString() },
            Action      = "LogoSet",
            Description = $"Admin set logo for tenant {id} (document {documentId}).",
            Metadata    = JsonSerializer.Serialize(new { tenantId = id, documentId, callerTenant }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.tenant.logo_set", id.ToString()),
            Tags = ["tenant", "logo", "branding"],
        });

        return Results.Ok(new { tenantId = tenant.Id, logoDocumentId = documentId, updatedAtUtc = tenant.UpdatedAtUtc });
    }

    /// <summary>
    /// DELETE /api/admin/tenants/{id}/logo
    /// Clears the tenant's logo, reverting to the platform default (LegalSynq) branding.
    /// </summary>
    private static async Task<IResult> ClearTenantLogo(
        Guid                id,
        ClaimsPrincipal     caller,
        IdentityDbContext   db,
        IAuditEventClient   auditClient,
        CancellationToken   ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return Results.NotFound();

        tenant.ClearLogo();
        await db.SaveChangesAsync(ct);

        var callerId     = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var callerEmail  = caller.FindFirstValue(ClaimTypes.Email) ?? caller.FindFirstValue("email");
        var now          = DateTimeOffset.UtcNow;

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.tenant.logo_cleared",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Platform,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = id.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = callerId,
                Type = ActorType.User,
                Name = callerEmail,
            },
            Entity      = new AuditEventEntityDto { Type = "Tenant", Id = id.ToString() },
            Action      = "LogoCleared",
            Description = $"Admin cleared logo for tenant {id}.",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.tenant.logo_cleared", id.ToString()),
            Tags = ["tenant", "logo", "branding"],
        });

        return Results.NoContent();
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
        string productCode,
        IdentityDbContext db,
        EntitlementRequest body,
        IAuditEventClient auditClient,
        IProductProvisioningService provisioningEngine)
    {
        if (!FrontendToDbProductCode.TryGetValue(productCode, out var dbCode))
            dbCode = productCode;

        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == id);
        if (!tenantExists) return Results.NotFound();

        var productExists = await db.Products.AnyAsync(p => p.Code == dbCode);
        if (!productExists)
            return Results.NotFound(new { error = $"Product '{productCode}' not found." });

        var result = await provisioningEngine.ProvisionAsync(
            new ProvisionProductRequest(id, dbCode, body.Enabled));

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
            tenantId    = id,
            productCode,
            enabled     = body.Enabled,
            status      = body.Enabled ? "Active" : "Disabled",
            provisioningResult = new
            {
                tenantProductCreated       = result.TenantProductCreated,
                organizationProductsCreated = result.OrganizationProductsCreated,
                organizationProductsUpdated = result.OrganizationProductsUpdated,
                handlerExecuted            = result.HandlerResult is not null,
            },
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
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

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
            isLocked          = u.IsLocked,
            lockedAtUtc       = u.LockedAtUtc,
            lastLoginAtUtc    = u.LastLoginAtUtc,
            sessionVersion    = u.SessionVersion,
            avatarDocumentId  = u.AvatarDocumentId,
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
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();

        // UIX-003-01: TenantAdmin may only deactivate users within their own tenant.
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

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
    // UIX-003-03: SECURITY / SESSION ADMIN ACTIONS
    // =========================================================================

    /// <summary>
    /// POST /api/admin/users/{id}/lock
    ///
    /// Administratively locks a user account. Locked users cannot authenticate.
    /// Also increments SessionVersion, invalidating all active JWTs.
    /// Idempotent: 204 if already locked.
    /// Emits identity.user.locked audit event.
    /// </summary>
    private static async Task<IResult> LockUser(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

        var callerIdStr = caller.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                       ?? caller.FindFirstValue("sub");
        var lockingAdminId = Guid.TryParse(callerIdStr, out var aid) ? (Guid?)aid : null;

        var changed = user.Lock(lockingAdminId);
        if (!changed) return Results.NoContent();

        await db.SaveChangesAsync(ct);

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.locked",
            EventCategory = EventCategory.Security,
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
                Id   = callerIdStr,
                Type = ActorType.User,
                Name = "admin",
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "UserLocked",
            Description = $"User '{user.Email}' locked by admin in tenant {user.TenantId}.",
            Before      = JsonSerializer.Serialize(new { isLocked = false, email = user.Email }),
            After       = JsonSerializer.Serialize(new { isLocked = true,  email = user.Email, lockedAt = now }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.locked", user.Id.ToString()),
            Tags = ["user-management", "security", "lock"],
        });

        return Results.NoContent();
    }

    /// <summary>
    /// POST /api/admin/users/{id}/unlock
    ///
    /// Unlocks an administratively locked account.
    /// Idempotent: 204 if already unlocked.
    /// Emits identity.user.unlocked audit event.
    /// </summary>
    private static async Task<IResult> UnlockUser(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

        var changed = user.Unlock();
        if (!changed) return Results.NoContent();

        await db.SaveChangesAsync(ct);

        var callerIdStr = caller.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                       ?? caller.FindFirstValue("sub");

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.unlocked",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = user.TenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = callerIdStr,
                Type = ActorType.User,
                Name = "admin",
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "UserUnlocked",
            Description = $"User '{user.Email}' unlocked by admin in tenant {user.TenantId}.",
            Before      = JsonSerializer.Serialize(new { isLocked = true,  email = user.Email }),
            After       = JsonSerializer.Serialize(new { isLocked = false, email = user.Email }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.unlocked", user.Id.ToString()),
            Tags = ["user-management", "security", "unlock"],
        });

        return Results.NoContent();
    }

    /// <summary>
    /// POST /api/admin/users/{id}/force-logout
    ///
    /// Revokes all active sessions for a user by incrementing their SessionVersion.
    /// All existing JWTs containing an older session_version will be rejected by auth/me.
    /// Emits identity.user.force_logout audit event.
    /// </summary>
    private static async Task<IResult> ForceLogout(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

        user.IncrementSessionVersion();
        await db.SaveChangesAsync(ct);

        var callerIdStr = caller.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                       ?? caller.FindFirstValue("sub");

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.force_logout",
            EventCategory = EventCategory.Security,
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
                Id   = callerIdStr,
                Type = ActorType.User,
                Name = "admin",
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "ForceLogout",
            Description = $"All sessions revoked for user '{user.Email}' in tenant {user.TenantId}.",
            Metadata    = JsonSerializer.Serialize(new { newSessionVersion = user.SessionVersion }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.force_logout", user.Id.ToString()),
            Tags = ["user-management", "security", "session", "force-logout"],
        });

        return Results.NoContent();
    }

    /// <summary>
    /// POST /api/admin/users/{id}/reset-password
    ///
    /// Admin-triggers a password reset workflow for a user.
    /// Creates a PasswordResetToken (24-hour expiry) and logs the reset link.
    /// In production, the link would be emailed; in dev it is logged only.
    ///
    /// Any previous pending reset tokens for this user are revoked first (idempotent).
    /// Emits identity.user.password_reset_triggered audit event.
    /// </summary>
    private static async Task<IResult> AdminResetPassword(
        Guid                id,
        ClaimsPrincipal     caller,
        IdentityDbContext   db,
        IAuditEventClient   auditClient,
        ILoggerFactory      loggerFactory,
        IHostEnvironment    env,
        CancellationToken   ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(AdminEndpoints));

        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

        // Revoke any existing pending reset tokens for this user.
        var existingTokens = await db.PasswordResetTokens
            .Where(t => t.UserId == id && t.Status == PasswordResetToken.Statuses.Pending)
            .ToListAsync(ct);
        foreach (var old in existingTokens) old.Revoke();

        // Generate a new cryptographically random reset token.
        var rawToken  = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(rawToken)));

        var callerIdStr = caller.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                       ?? caller.FindFirstValue("sub");
        var triggeredByAdminId = Guid.TryParse(callerIdStr, out var aid) ? (Guid?)aid : null;

        var resetToken = PasswordResetToken.Create(id, user.TenantId, tokenHash, triggeredByAdminId);
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync(ct);

        // In dev: log the raw token for convenience without email infrastructure.
        // In production the token must be delivered to the user via email (never logged).
        if (env.IsDevelopment())
        {
            logger.LogDebug(
                "[UIX-003-03] DEV password reset token for user {UserId} ({Email}): {RawToken} (expires {ExpiresAt:O}).",
                user.Id, user.Email, rawToken, resetToken.ExpiresAtUtc);
        }
        else
        {
            logger.LogInformation(
                "[UIX-003-03] Admin-triggered password reset for user {UserId} ({Email}) in tenant {TenantId}. Reset token ID: {TokenId}.",
                user.Id, user.Email, user.TenantId, resetToken.Id);
        }

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.password_reset_triggered",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = user.TenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = callerIdStr,
                Type = ActorType.User,
                Name = "admin",
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "PasswordResetTriggered",
            Description = $"Admin-triggered password reset for user '{user.Email}' in tenant {user.TenantId}.",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.password_reset_triggered", user.Id.ToString()),
            Tags = ["user-management", "security", "password-reset"],
        });

        return Results.Ok(new { message = "Password reset email will be sent to the user." });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/set-password
    ///
    /// Admin-sets a new password directly for a user. The password must be at
    /// least 8 characters. The user's session version is bumped so all existing
    /// sessions are invalidated.
    ///
    /// Access: PlatformAdmin only. Tenant-scoped for TenantAdmin callers.
    /// Emits identity.user.password_set_by_admin audit event.
    /// </summary>
    private static async Task<IResult> AdminSetPassword(
        Guid              id,
        SetPasswordRequest body,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IPasswordHasher   passwordHasher,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 12)
            return Results.BadRequest(new { error = "Password must be at least 12 characters." });

        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

        var hash = passwordHasher.Hash(body.NewPassword);
        user.SetPassword(hash);

        await db.SaveChangesAsync(ct);

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.password_set_by_admin",
            EventCategory = EventCategory.Security,
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
                Type = ActorType.User,
                Name = "admin",
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "PasswordSetByAdmin",
            Description = $"Admin directly set a new password for user '{user.Email}' in tenant {user.TenantId}.",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.password_set_by_admin", user.Id.ToString()),
            Tags = ["user-management", "security", "password-set"],
        });

        return Results.Ok(new { message = "Password updated successfully." });
    }

    /// <summary>
    /// GET /api/admin/users/{id}/security
    ///
    /// Returns a security summary for the user: lock state, last login,
    /// session version, and recent security/admin audit events.
    /// Read-only. Tenant-scoped for TenantAdmin callers.
    /// </summary>
    private static async Task<IResult> GetUserSecurity(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var u = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (u is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, u.TenantId)) return Results.Forbid();

        var hasPendingInvite = await db.UserInvitations
            .AnyAsync(i => i.UserId == id && i.Status == UserInvitation.Statuses.Pending, ct);

        var recentResets = await db.PasswordResetTokens
            .Where(t => t.UserId == id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(5)
            .Select(t => new
            {
                id          = t.Id,
                status      = t.Status,
                createdAt   = t.CreatedAtUtc,
                expiresAt   = t.ExpiresAtUtc,
                usedAt      = t.UsedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            userId           = u.Id,
            email            = u.Email,
            isLocked         = u.IsLocked,
            lockedAtUtc      = u.LockedAtUtc,
            lastLoginAtUtc   = u.LastLoginAtUtc,
            sessionVersion   = u.SessionVersion,
            isActive         = u.IsActive,
            hasPendingInvite,
            recentPasswordResets = recentResets,
            // Recent security/admin events are fetched by the CC frontend via the
            // Audit service query API (/audit/entity/User/{userId}) to avoid coupling
            // the identity service to the audit DB.
        });
    }

    /// <summary>
    /// UIX-004: GET /api/admin/users/{id}/activity
    ///
    /// Returns a paged list of local audit log entries (AuditLogs table) for
    /// the specified user. Covers admin actions emitted by the Identity service:
    /// lock, unlock, force-logout, password reset, role assignment, membership changes.
    ///
    /// For richer canonical events (login, logout, invite-accepted, etc.) the CC
    /// queries the Audit service directly via /audit-service/audit/events?targetId=.
    ///
    /// Scope: TenantAdmin sees only users in their tenant. PlatformAdmin sees all.
    /// </summary>
    private static async Task<IResult> GetUserActivity(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        int    page     = 1,
        int    pageSize = 20,
        string category = "",
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var u = await db.Users
            .AsNoTracking()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (u is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, u.TenantId)) return Results.Forbid();

        var idStr = id.ToString();
        var q = db.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityId == idStr);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(a => a.EntityType == category);

        var total = await q.CountAsync(ct);

        // Materialize with raw MetadataJson string first — EF Core cannot translate
        // JsonSerializer.Deserialize (it has optional parameters) inside an expression tree.
        var rawRows = await q
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
            .ToListAsync(ct);

        // Deserialize metadata in-memory after materialization.
        var rows = rawRows.Select(a => new
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
            items      = rows,
            totalCount = total,
            page,
            pageSize,
        });
    }

    // =========================================================================
    // LSCC-01-003: CareConnect provider provisioning
    // =========================================================================

    private static readonly Guid CcProductId          = new("10000000-0000-0000-0000-000000000003");
    private static readonly Guid CcReceiverRoleId      = new("50000000-0000-0000-0000-000000000002");
    private static readonly Guid CcReferrerRoleId      = new("50000000-0000-0000-0000-000000000001");

    /// <summary>
    /// GET /api/admin/users/{id}/careconnect-readiness
    /// Returns a diagnostic snapshot of the four conditions required before a user's
    /// provider organization can receive CareConnect referrals.
    /// </summary>
    private static async Task<IResult> GetCareConnectReadiness(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        CancellationToken ct)
    {
        var u = await db.Users
            .AsNoTracking()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (u is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, u.TenantId)) return Results.Forbid();

        // ── Primary org membership ────────────────────────────────────────────
        var membership = await db.UserOrganizationMemberships
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == id && m.IsPrimary && m.IsActive)
            .FirstOrDefaultAsync(ct);

        bool hasPrimaryOrg = membership is not null;
        var  orgId         = membership?.OrganizationId;
        var  orgType       = membership?.Organization?.OrgType;

        // ── Tenant-level CareConnect entitlement ─────────────────────────────
        bool tenantHasCareConnect = await db.Set<Identity.Domain.TenantProduct>()
            .AsNoTracking()
            .AnyAsync(tp => tp.TenantId == u.TenantId
                         && tp.ProductId == CcProductId
                         && tp.IsEnabled, ct);

        // ── Org-level CareConnect entitlement ────────────────────────────────
        bool orgHasCareConnect = false;
        if (orgId.HasValue)
        {
            orgHasCareConnect = await db.OrganizationProducts
                .AsNoTracking()
                .AnyAsync(op => op.OrganizationId == orgId.Value
                              && op.ProductId == CcProductId
                              && op.IsEnabled, ct);
        }

        // ── CareConnect role (RECEIVER or REFERRER) ───────────────────────────
        bool hasCareConnectRole = await db.ScopedRoleAssignments
            .AsNoTracking()
            .AnyAsync(s => s.UserId == id
                        && s.IsActive
                        && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
                        && (s.RoleId == CcReceiverRoleId || s.RoleId == CcReferrerRoleId), ct);

        bool isFullyProvisioned = hasPrimaryOrg && tenantHasCareConnect && orgHasCareConnect && hasCareConnectRole;

        return Results.Ok(new
        {
            userId                = id,
            hasPrimaryOrg,
            primaryOrgId          = orgId,
            primaryOrgType        = orgType,
            tenantHasCareConnect,
            orgHasCareConnect,
            hasCareConnectRole,
            isFullyProvisioned,
        });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/provision-careconnect
    /// Idempotent. Ensures:
    ///   1. Tenant has SYNQ_CARECONNECT TenantProduct (enabled).
    ///   2. User's primary org has SYNQ_CARECONNECT OrganizationProduct (enabled).
    ///   3. User has the CARECONNECT_RECEIVER ScopedRoleAssignment (global).
    /// Returns a summary of what was done vs already in place.
    /// </summary>
    private static async Task<IResult> ProvisionForCareConnect(
        Guid              id,
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IProductProvisioningService provisioningEngine,
        CancellationToken ct)
    {
        var callerId = caller.FindFirstValue(ClaimTypes.NameIdentifier) is { } cid
                       && Guid.TryParse(cid, out var cGuid) ? cGuid : (Guid?)null;

        var u = await db.Users
            .AsNoTracking()
            .Include(u => u.Tenant)
                .ThenInclude(t => t.TenantProducts)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (u is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, u.TenantId)) return Results.Forbid();

        var membership = await db.UserOrganizationMemberships
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == id && m.IsPrimary && m.IsActive)
            .FirstOrDefaultAsync(ct);

        if (membership is null)
            return Results.UnprocessableEntity(new
            {
                error = "User does not have an active primary organization membership. " +
                        "Link the user to a PROVIDER org first.",
                code  = "NO_PRIMARY_ORG",
            });

        var org = await db.Organizations
            .Include(o => o.OrganizationProducts)
            .FirstOrDefaultAsync(o => o.Id == membership.OrganizationId, ct);

        if (org is null)
            return Results.UnprocessableEntity(new
            {
                error = "Primary organization record not found.",
                code  = "ORG_NOT_FOUND",
            });

        var provResult = await provisioningEngine.ProvisionAsync(
            new ProvisionProductRequest(u.TenantId, ProductCodes.SynqCareConnect, true), ct);

        bool roleAdded = false;
        var existingRole = await db.ScopedRoleAssignments
            .FirstOrDefaultAsync(s => s.UserId == id
                                   && s.IsActive
                                   && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
                                   && (s.RoleId == CcReceiverRoleId || s.RoleId == CcReferrerRoleId), ct);

        if (existingRole is null)
        {
            var sra = ScopedRoleAssignment.Create(
                userId:           id,
                roleId:           CcReceiverRoleId,
                scopeType:        ScopedRoleAssignment.ScopeTypes.Global,
                tenantId:         u.TenantId,
                assignedByUserId: callerId);
            db.ScopedRoleAssignments.Add(sra);
            roleAdded = true;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new
        {
            userId             = id,
            organizationId     = org.Id,
            organizationName   = org.DisplayName ?? org.Name,
            tenantProductAdded = provResult.TenantProductCreated,
            orgProductAdded    = provResult.OrganizationProductsCreated > 0 || provResult.OrganizationProductsUpdated > 0,
            roleAdded,
            isFullyProvisioned = true,
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
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var total = await db.Roles.CountAsync();

        // UIX-005: materialize roles first, then join capability counts
        // (EF Core LINQ restriction: cannot call Count inside Select on nav collections)
        var roleList = await db.Roles
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var roleIds = roleList.Select(r => r.Id).ToList();

        var userCounts = await db.ScopedRoleAssignments
            .Where(s => roleIds.Contains(s.RoleId) && s.IsActive
                     && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
            .GroupBy(s => s.RoleId)
            .Select(g => new { roleId = g.Key, count = g.Count() })
            .ToListAsync();

        var capCounts = await db.RoleCapabilityAssignments
            .Where(a => roleIds.Contains(a.RoleId))
            .GroupBy(a => a.RoleId)
            .Select(g => new { roleId = g.Key, count = g.Count() })
            .ToListAsync();

        var userCountMap = userCounts.ToDictionary(x => x.roleId, x => x.count);
        var capCountMap  = capCounts.ToDictionary(x => x.roleId, x => x.count);

        // UIX-002-C: resolve product metadata for non-system roles
        var productRoles = await db.ProductRoles
            .Include(pr => pr.Product)
            .Include(pr => pr.OrgTypeRules)
                .ThenInclude(r => r.OrganizationType)
            .Where(pr => pr.IsActive)
            .ToListAsync();
        var prLookup = productRoles.ToDictionary(pr => pr.Code, StringComparer.OrdinalIgnoreCase);

        var roles = roleList.Select(r =>
        {
            prLookup.TryGetValue(r.Name, out var pr);
            var isProductRole = !r.IsSystemRole && pr is not null;
            return new
            {
                id              = r.Id,
                name            = r.Name,
                description     = r.Description ?? "",
                isSystemRole    = r.IsSystemRole,
                isProductRole,
                productCode     = isProductRole ? pr!.Product.Code : (string?)null,
                productName     = isProductRole ? pr!.Product.Name : (string?)null,
                allowedOrgTypes = isProductRole
                    ? pr!.OrgTypeRules.Where(rule => rule.IsActive).Select(rule => rule.OrganizationType.Code).ToArray()
                    : null,
                userCount       = userCountMap.GetValueOrDefault(r.Id, 0),
                capabilityCount = capCountMap.GetValueOrDefault(r.Id, 0),
                permissions     = Array.Empty<string>(),
            };
        });

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

        var userCount = await db.ScopedRoleAssignments
            .CountAsync(s => s.RoleId == id && s.IsActive
                          && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global);

        // UIX-005: load actual capability assignments for this role
        var caps = await db.RoleCapabilityAssignments
            .Where(a => a.RoleId == id)
            .Include(a => a.Capability)
            .ThenInclude(c => c.Product)
            .OrderBy(a => a.Capability.Product.Name)
            .ThenBy(a => a.Capability.Code)
            .ToListAsync();

        var resolvedPermissions = caps.Select(a => new
        {
            id          = a.CapabilityId,
            key         = a.Capability.Code,
            description = a.Capability.Description ?? a.Capability.Name,
            name        = a.Capability.Name,
            productId   = a.Capability.ProductId,
            productName = a.Capability.Product.Name,
        }).ToList();

        return Results.Ok(new
        {
            id                  = r.Id,
            name                = r.Name,
            description         = r.Description ?? "",
            isSystemRole        = r.IsSystemRole,
            userCount,
            capabilityCount     = caps.Count,
            permissions         = resolvedPermissions.Select(p => p.key).ToArray(),
            resolvedPermissions,
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
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

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
        new("require_availability_check","Require Availability Check", false, "boolean", "When enabled, law firms must verify provider availability before creating a referral. When disabled, referrals can be sent to any provider.", true),
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
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

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

        // UIX-003-01: TenantAdmin may only assign roles within their own tenant.
        if (IsCrossTenantAccess(ctx.User, user.TenantId)) return Results.Forbid();

        var role = await db.Roles.FindAsync(body.RoleId);
        if (role is null) return Results.NotFound(new { error = $"Role '{body.RoleId}' not found." });

        // ── UIX-002-C: Product Role Eligibility Guardrails ──────────────────
        // If this role maps to a ProductRole (IsSystemRole == false and name matches
        // a ProductRole code), enforce org-type and product-enablement rules.
        if (!role.IsSystemRole)
        {
            var productRole = await db.ProductRoles
                .Include(pr => pr.Product)
                .Include(pr => pr.OrgTypeRules)
                    .ThenInclude(r => r.OrganizationType)
                .FirstOrDefaultAsync(pr => pr.Code == role.Name && pr.IsActive);

            if (productRole is not null)
            {
                // 1. Tenant product enablement check
                var tenantHasProduct = await db.TenantProducts
                    .AnyAsync(tp => tp.TenantId == user.TenantId
                                 && tp.ProductId == productRole.ProductId
                                 && tp.IsEnabled);
                if (!tenantHasProduct)
                    return Results.BadRequest(new
                    {
                        error = "PRODUCT_NOT_ENABLED_FOR_TENANT",
                        message = $"Product '{productRole.Product.Name}' is not enabled for this user's tenant. " +
                                  "Enable the product entitlement before assigning this role.",
                    });

                // 2. Org type eligibility check
                var primaryMembership = await db.UserOrganizationMemberships
                    .Include(m => m.Organization)
                    .Where(m => m.UserId == id && m.IsActive)
                    .OrderByDescending(m => m.IsPrimary)
                    .ThenBy(m => m.JoinedAtUtc)
                    .FirstOrDefaultAsync();

                if (primaryMembership is null)
                    return Results.BadRequest(new
                    {
                        error = "NO_ORGANIZATION_MEMBERSHIP",
                        message = "User must belong to an organization before product roles can be assigned.",
                    });

                var userOrgTypeId = primaryMembership.Organization.OrganizationTypeId;
                var userOrgType   = primaryMembership.Organization.OrgType;

                if (productRole.OrgTypeRules.Count > 0)
                {
                    var orgTypeAllowed = userOrgTypeId.HasValue
                        ? productRole.OrgTypeRules.Any(r => r.IsActive && r.OrganizationTypeId == userOrgTypeId.Value)
                        : productRole.OrgTypeRules.Any(r => r.IsActive &&
                            r.OrganizationType.Code.Equals(userOrgType, StringComparison.OrdinalIgnoreCase));

                    if (!orgTypeAllowed)
                    {
                        var allowedTypes = productRole.OrgTypeRules
                            .Where(r => r.IsActive)
                            .Select(r => r.OrganizationType.Code)
                            .ToList();

                        return Results.BadRequest(new
                        {
                            error = "INVALID_ORG_TYPE_FOR_ROLE",
                            message = $"Role '{productRole.Name}' requires org type [{string.Join(", ", allowedTypes)}] " +
                                      $"but user's organization is '{userOrgType}'.",
                        });
                    }
                }
            }
        }

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
    /// GET /api/admin/users/{id}/assignable-roles
    ///
    /// UIX-002-C: Returns all roles (system + product) with eligibility metadata
    /// for a specific user. Product roles include org-type and tenant product
    /// enablement checks based on the user's primary organization.
    /// </summary>
    private static async Task<IResult> GetAssignableRoles(
        Guid              id,
        IdentityDbContext db,
        HttpContext        ctx)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        if (IsCrossTenantAccess(ctx.User, user.TenantId)) return Results.Forbid();

        // Resolve user's primary org type
        var primaryMembership = await db.UserOrganizationMemberships
            .Include(m => m.Organization)
            .Where(m => m.UserId == id && m.IsActive)
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.JoinedAtUtc)
            .FirstOrDefaultAsync();

        var userOrgType   = primaryMembership?.Organization.OrgType;
        var userOrgTypeId = primaryMembership?.Organization.OrganizationTypeId;

        // Get enabled products for this tenant
        var enabledProductIds = await db.TenantProducts
            .Where(tp => tp.TenantId == user.TenantId && tp.IsEnabled)
            .Select(tp => tp.ProductId)
            .ToListAsync();
        var enabledProductSet = new HashSet<Guid>(enabledProductIds);

        // Get all product roles with their rules
        var productRoles = await db.ProductRoles
            .Include(pr => pr.Product)
            .Include(pr => pr.OrgTypeRules)
                .ThenInclude(r => r.OrganizationType)
            .Where(pr => pr.IsActive)
            .ToListAsync();

        // Build code → ProductRole lookup
        var productRoleLookup = productRoles.ToDictionary(pr => pr.Code, StringComparer.OrdinalIgnoreCase);

        // All roles
        var allRoles = await db.Roles
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync();

        // Currently assigned role IDs
        var assignedRoleIds = await db.ScopedRoleAssignments
            .Where(s => s.UserId == id && s.IsActive
                     && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
            .Select(s => s.RoleId)
            .ToListAsync();
        var assignedSet = new HashSet<Guid>(assignedRoleIds);

        var result = allRoles.Select(r =>
        {
            var isAssigned = assignedSet.Contains(r.Id);
            productRoleLookup.TryGetValue(r.Name, out var pr);
            var isProductRole = !r.IsSystemRole && pr is not null;

            string? productCode = null;
            string? productName = null;
            List<string>? allowedOrgTypes = null;
            var assignable = true;
            string? disabledReason = null;

            if (isProductRole && pr is not null)
            {
                productCode = pr.Product.Code;
                productName = pr.Product.Name;
                allowedOrgTypes = pr.OrgTypeRules
                    .Where(rule => rule.IsActive)
                    .Select(rule => rule.OrganizationType.Code)
                    .ToList();

                // Check product enablement
                if (!enabledProductSet.Contains(pr.ProductId))
                {
                    assignable = false;
                    disabledReason = $"Product '{pr.Product.Name}' is not enabled for this tenant.";
                }
                // Check org type eligibility
                else if (primaryMembership is null)
                {
                    assignable = false;
                    disabledReason = "User has no organization membership.";
                }
                else if (allowedOrgTypes.Count > 0)
                {
                    var orgTypeAllowed = userOrgTypeId.HasValue
                        ? pr.OrgTypeRules.Any(rule => rule.IsActive && rule.OrganizationTypeId == userOrgTypeId.Value)
                        : pr.OrgTypeRules.Any(rule => rule.IsActive &&
                            rule.OrganizationType.Code.Equals(userOrgType ?? "", StringComparison.OrdinalIgnoreCase));

                    if (!orgTypeAllowed)
                    {
                        assignable = false;
                        disabledReason = $"Requires org type: {string.Join(", ", allowedOrgTypes)}. User org is '{userOrgType}'.";
                    }
                }
            }

            if (isAssigned)
            {
                assignable = false;
                disabledReason = "Already assigned.";
            }

            return new
            {
                id             = r.Id,
                name           = r.Name,
                description    = r.Description ?? "",
                isSystemRole   = r.IsSystemRole,
                isProductRole,
                productCode,
                productName,
                allowedOrgTypes,
                assignable,
                disabledReason,
                isAssigned,
            };
        });

        return Results.Ok(new
        {
            items       = result,
            userOrgType = userOrgType ?? "UNKNOWN",
            tenantEnabledProducts = enabledProductIds.Count,
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
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IAuditEventClient auditClient)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        // UIX-003-01: TenantAdmin may only revoke roles within their own tenant.
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

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
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        // UIX-003-01: TenantAdmin may only activate users within their own tenant.
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

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
        ClaimsPrincipal   caller,
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

        // UIX-003-01: TenantAdmin may only invite users into their own tenant.
        if (IsCrossTenantAccess(caller, body.TenantId)) return Results.Forbid();

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
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        // UIX-003-01: TenantAdmin may only resend invites for users in their own tenant.
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

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
        ClaimsPrincipal         caller,
        IdentityDbContext       db,
        CancellationToken       ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        // UIX-003-01: TenantAdmin may only assign memberships within their own tenant.
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

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
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        CancellationToken ct)
    {
        // UIX-003-01: load user to enforce TenantAdmin tenant boundary.
        var user = await db.Users.FindAsync([id], ct);
        if (user is not null && IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

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
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        CancellationToken ct)
    {
        // UIX-003-01: load user to enforce TenantAdmin tenant boundary.
        var user = await db.Users.FindAsync([id], ct);
        if (user is not null && IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

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
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

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
        ClaimsPrincipal        caller,
        IdentityDbContext      db,
        CancellationToken      ct)
    {
        var group = await db.TenantGroups.FindAsync([id], ct);
        if (group is null) return Results.NotFound(new { error = $"Group '{id}' not found." });

        // UIX-003-01: TenantAdmin may only manage members in their own tenant's groups.
        if (IsCrossTenantAccess(caller, group.TenantId)) return Results.Forbid();

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
        ClaimsPrincipal   caller,
        IdentityDbContext db,
        CancellationToken ct)
    {
        // UIX-003-01: load group to enforce TenantAdmin tenant boundary.
        var group = await db.TenantGroups.FindAsync([id], ct);
        if (group is not null && IsCrossTenantAccess(caller, group.TenantId)) return Results.Forbid();

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
        string search    = "",
        CancellationToken ct = default)
    {
        var q = db.Capabilities
            .Include(c => c.Product)
            .Where(c => c.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(productId) && Guid.TryParse(productId, out var pid))
            q = q.Where(c => c.ProductId == pid);

        // UIX-005: simple substring search across code, name, and description
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLowerInvariant();
            q = q.Where(c =>
                c.Code.Contains(s)       ||
                c.Name.Contains(s)       ||
                (c.Description != null && c.Description.Contains(s)));
        }

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

    // =========================================================================
    // ROLE PERMISSION MANAGEMENT (UIX-005)
    // =========================================================================

    /// <summary>
    /// GET /api/admin/roles/{id}/permissions
    ///
    /// Returns all capabilities assigned to a role.
    /// Access: PlatformAdmin (any role) or TenantAdmin (own-tenant non-system roles; system roles readable).
    /// UIX-005-01: Added caller + cross-tenant boundary enforcement.
    /// </summary>
    private static async Task<IResult> GetRolePermissions(
        Guid              id,
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        CancellationToken ct = default)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return Results.NotFound();

        // Cross-tenant guard: TenantAdmin may not read non-system roles from other tenants.
        // System roles are global (readable by all authenticated admins).
        if (!role.IsSystemRole && IsCrossTenantAccess(caller, role.TenantId))
            return Results.Forbid();

        var assignments = await db.RoleCapabilityAssignments
            .Where(a => a.RoleId == id)
            .Include(a => a.Capability)
            .ThenInclude(c => c.Product)
            .OrderBy(a => a.Capability.Product.Name)
            .ThenBy(a => a.Capability.Code)
            .ToListAsync(ct);

        var items = assignments.Select(a => new
        {
            id               = a.CapabilityId,
            code             = a.Capability.Code,
            name             = a.Capability.Name,
            description      = a.Capability.Description,
            productId        = a.Capability.ProductId,
            productName      = a.Capability.Product.Name,
            isActive         = a.Capability.IsActive,
            assignedAtUtc    = a.AssignedAtUtc,
            assignedByUserId = a.AssignedByUserId,
        }).ToList();

        return Results.Ok(new { items, totalCount = items.Count });
    }

    private record AssignRolePermissionRequest(Guid CapabilityId);

    /// <summary>
    /// POST /api/admin/roles/{id}/permissions
    ///
    /// Assigns a capability to a role. Idempotent — returns 200 if already assigned.
    /// Emits a role.permission.assigned audit event.
    /// Access: PlatformAdmin only.
    /// </summary>
    private static async Task<IResult> AssignRolePermission(
        Guid                         id,
        AssignRolePermissionRequest  body,
        IdentityDbContext            db,
        ClaimsPrincipal              caller,
        IAuditEventClient            auditClient,
        ILoggerFactory               loggerFactory,
        CancellationToken            ct = default)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoints.AssignRolePermission");

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return Results.NotFound(new { error = "Role not found." });

        // UIX-005-01: system roles may only be modified by PlatformAdmin
        if (role.IsSystemRole && !caller.IsInRole("PlatformAdmin"))
            return Results.Json(new { error = "System roles cannot be modified. Contact the platform administrator." }, statusCode: 403);

        // UIX-005-01: TenantAdmin may not assign permissions to roles outside their tenant
        if (IsCrossTenantAccess(caller, role.TenantId))
            return Results.Forbid();

        var capability = await db.Capabilities.FirstOrDefaultAsync(c => c.Id == body.CapabilityId && c.IsActive, ct);
        if (capability is null) return Results.NotFound(new { error = "Capability not found or inactive." });

        // Idempotency: return OK if already assigned
        var alreadyAssigned = await db.RoleCapabilityAssignments
            .AnyAsync(a => a.RoleId == id && a.CapabilityId == body.CapabilityId, ct);

        if (alreadyAssigned)
            return Results.Ok(new { message = "Capability already assigned to role." });

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId  = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        var assignment = RoleCapabilityAssignment.Create(id, body.CapabilityId, callerId);
        db.RoleCapabilityAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Role {RoleId} assigned capability {CapabilityId} by {ActorId}",
            id, body.CapabilityId, callerId);

        var assignAuditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "role.permission.assigned",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = assignAuditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Role", Id = id.ToString() },
            Action        = "PermissionAssigned",
            Description   = $"Capability '{capability.Code}' assigned to role '{role.Name}'",
            Metadata      = System.Text.Json.JsonSerializer.Serialize(new
            {
                roleId       = id,
                capabilityId = body.CapabilityId,
                code         = capability.Code,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(assignAuditNow, "identity-service", "role.permission.assigned", id.ToString(), body.CapabilityId.ToString()),
        });

        return Results.Created(
            $"/api/admin/roles/{id}/permissions/{body.CapabilityId}",
            new { roleId = id, capabilityId = body.CapabilityId });
    }

    /// <summary>
    /// DELETE /api/admin/roles/{id}/permissions/{capabilityId}
    ///
    /// Revokes a capability from a role. Emits a role.permission.revoked audit event.
    /// Access: PlatformAdmin only.
    /// </summary>
    private static async Task<IResult> RevokeRolePermission(
        Guid              id,
        Guid              capabilityId,
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        IAuditEventClient auditClient,
        ILoggerFactory    loggerFactory,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoints.RevokeRolePermission");

        var assignment = await db.RoleCapabilityAssignments
            .Include(a => a.Capability)
            .Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.RoleId == id && a.CapabilityId == capabilityId, ct);

        if (assignment is null)
            return Results.NotFound(new { error = "Permission assignment not found." });

        // UIX-005-01: system roles may only be modified by PlatformAdmin
        if (assignment.Role.IsSystemRole && !caller.IsInRole("PlatformAdmin"))
            return Results.Json(new { error = "System roles cannot be modified. Contact the platform administrator." }, statusCode: 403);

        // UIX-005-01: TenantAdmin may not revoke permissions from roles outside their tenant
        if (IsCrossTenantAccess(caller, assignment.Role.TenantId))
            return Results.Forbid();

        db.RoleCapabilityAssignments.Remove(assignment);
        await db.SaveChangesAsync(ct);

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);

        logger.LogInformation(
            "Role {RoleId} revoked capability {CapabilityId} by {ActorId}",
            id, capabilityId, callerIdRaw);

        var revokeAuditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "role.permission.revoked",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = revokeAuditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Role", Id = id.ToString() },
            Action        = "PermissionRevoked",
            Description   = $"Capability '{assignment.Capability.Code}' revoked from role '{assignment.Role.Name}'",
            Metadata      = System.Text.Json.JsonSerializer.Serialize(new
            {
                roleId       = id,
                capabilityId,
                code         = assignment.Capability.Code,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(revokeAuditNow, "identity-service", "role.permission.revoked", id.ToString(), capabilityId.ToString()),
        });

        return Results.NoContent();
    }

    /// <summary>
    /// GET /api/admin/users/{id}/permissions
    ///
    /// Returns the effective (union) permissions for a user, derived from all active
    /// role assignments. Each capability includes which role(s) grant it.
    /// Access: PlatformAdmin or TenantAdmin (with tenant boundary check).
    /// </summary>
    private static async Task<IResult> GetUserEffectivePermissions(
        Guid              id,
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();

        if (IsCrossTenantAccess(caller, user.TenantId))
            return Results.Forbid();

        // Active global-scoped role assignments for this user
        var roleAssignments = await db.ScopedRoleAssignments
            .Where(s => s.UserId == id && s.IsActive
                     && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
            .Include(s => s.Role)
            .ToListAsync(ct);

        if (roleAssignments.Count == 0)
            return Results.Ok(new { items = Array.Empty<object>(), totalCount = 0, roleCount = 0 });

        var roleIds = roleAssignments.Select(s => s.RoleId).ToList();

        // Capability assignments for all those roles
        var capAssignments = await db.RoleCapabilityAssignments
            .Where(a => roleIds.Contains(a.RoleId))
            .Include(a => a.Capability)
            .ThenInclude(c => c.Product)
            .ToListAsync(ct);

        // Build map: capabilityId → list of role names that grant it
        var capToRoles = capAssignments
            .GroupBy(a => a.CapabilityId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => roleAssignments
                        .First(r => r.RoleId == a.RoleId).Role.Name)
                      .Distinct()
                      .ToList());

        // Distinct capabilities
        var distinctCaps = capAssignments
            .GroupBy(a => a.CapabilityId)
            .Select(g => g.First().Capability)
            .OrderBy(c => c.Product.Name)
            .ThenBy(c => c.Code)
            .ToList();

        var items = distinctCaps.Select(c => new
        {
            id          = c.Id,
            code        = c.Code,
            name        = c.Name,
            description = c.Description,
            productId   = c.ProductId,
            productName = c.Product.Name,
            isActive    = c.IsActive,
            sources     = capToRoles.GetValueOrDefault(c.Id, [])
                            .Select(roleName => new { type = "role", name = roleName })
                            .ToList(),
        }).ToList();

        return Results.Ok(new
        {
            items,
            totalCount = items.Count,
            roleCount  = roleAssignments.Count,
        });
    }

    // ── UIX-003-01: Caller-based tenant boundary enforcement ─────────────────

    /// <summary>
    /// Returns <c>true</c> if the caller is a non-PlatformAdmin whose tenant differs
    /// from <paramref name="targetTenantId"/>.  PlatformAdmins are never restricted.
    /// A caller with no parseable <c>tenant_id</c> claim is treated as cross-tenant (deny).
    /// </summary>
    private static bool IsCrossTenantAccess(ClaimsPrincipal caller, Guid targetTenantId)
    {
        if (caller.IsInRole("PlatformAdmin")) return false;
        var raw = caller.FindFirstValue("tenant_id");
        return raw is null || !Guid.TryParse(raw, out var callerTid) || callerTid != targetTenantId;
    }

    // ── UIX-003: Organizations list ──────────────────────────────────────────

    /// <summary>
    /// GET /api/admin/organizations?tenantId=
    ///
    /// Returns active organizations optionally filtered by tenantId.
    /// Used by the Control Center access-control panels to populate
    /// the "Add Membership" org selection dropdown.
    /// </summary>
    private static async Task<IResult> ListOrganizations(
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        string            tenantId = "",
        CancellationToken ct       = default)
    {
        var isPlatformAdmin = caller.IsInRole("PlatformAdmin");
        var callerTenantId  = caller.FindFirstValue("tenant_id");

        var q = db.Organizations.AsNoTracking().AsQueryable();

        // TenantAdmin is scoped to their own tenant.
        if (!isPlatformAdmin && callerTenantId is not null && Guid.TryParse(callerTenantId, out var callerTid))
        {
            q = q.Where(o => o.TenantId == callerTid);
        }
        else if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var filterTid))
        {
            q = q.Where(o => o.TenantId == filterTid);
        }

        var items = await q
            .Where(o => o.IsActive)
            .OrderBy(o => o.DisplayName ?? o.Name)
            .Select(o => new
            {
                id          = o.Id,
                tenantId    = o.TenantId,
                name        = o.Name,
                displayName = o.DisplayName ?? o.Name,
                orgType     = o.OrgType,
                isActive    = o.IsActive,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount = items.Count });
    }

    private static async Task<IResult> UpdateOrganization(
        Guid              id,
        UpdateOrganizationRequest body,
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        CancellationToken ct)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null) return Results.NotFound(new { error = $"Organization '{id}' not found." });

        Guid? resolvedTypeId = null;
        if (!string.IsNullOrWhiteSpace(body.OrgType))
        {
            if (!Domain.OrgType.IsValid(body.OrgType))
                return Results.BadRequest(new { error = $"Invalid OrgType: {body.OrgType}. Valid: {string.Join(", ", Domain.OrgType.All)}" });
            resolvedTypeId = OrgTypeMapper.TryResolve(body.OrgType);
        }

        org.Update(
            name:               body.Name ?? org.Name,
            displayName:        body.DisplayName ?? org.DisplayName,
            updatedByUserId:    null,
            organizationTypeId: resolvedTypeId,
            orgTypeCode:        body.OrgType);

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id          = org.Id,
            tenantId    = org.TenantId,
            name        = org.Name,
            displayName = org.DisplayName ?? org.Name,
            orgType     = org.OrgType,
            isActive    = org.IsActive,
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
    private record CreateTenantRequest(
        string  Name,
        string  Code,
        string  AdminEmail,
        string  AdminFirstName,
        string  AdminLastName,
        string? OrgType = null,
        string? PreferredSubdomain = null,
        List<string>? Products = null);
    private record InfraSubdomainRequest(string Subdomain);
    private record SetPasswordRequest(string NewPassword);
    private record EntitlementRequest(bool Enabled);
    private record SessionSettingsRequest(int? SessionTimeoutMinutes);
    private record UpdateOrganizationRequest(
        string? Name        = null,
        string? DisplayName = null,
        string? OrgType     = null);
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
