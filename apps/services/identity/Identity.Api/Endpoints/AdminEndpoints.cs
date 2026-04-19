using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Authorization.Filters;
using PermCodes = BuildingBlocks.Authorization.PermissionCodes;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        routes.MapPatch("/api/admin/tenants/{id:guid}/logo",             SetTenantLogo);
        routes.MapDelete("/api/admin/tenants/{id:guid}/logo",            ClearTenantLogo);
        routes.MapPatch("/api/admin/tenants/{id:guid}/logo-white",       SetTenantLogoWhite);
        routes.MapDelete("/api/admin/tenants/{id:guid}/logo-white",      ClearTenantLogoWhite);
        routes.MapPost("/api/admin/tenants/{id:guid}/provisioning/retry", RetryProvisioning);
        routes.MapPost("/api/admin/tenants/{id:guid}/verification/retry", RetryVerification);

        // ── Infrastructure DNS ──────────────────────────────────────────
        routes.MapPost("/api/admin/dns/provision", ProvisionInfraSubdomain);

        // ── Users ─────────────────────────────────────────────────────────
        routes.MapGet("/api/admin/users",           ListUsers);
        routes.MapGet("/api/admin/users/{id:guid}", GetUser);

        // ── Roles ──────────────────────────────────────────────────────────
        routes.MapGet("/api/admin/roles",           ListRoles);
        routes.MapGet("/api/admin/roles/{id:guid}", GetRole);

        // ── Products catalog (tenant-accessible) ────────────────────────
        routes.MapGet("/api/admin/products",        ListProducts);

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
        routes.MapGet("/api/admin/organizations",           ListOrganizations);
        routes.MapPost("/api/admin/organizations",          AdminEndpointsLscc010.CreateProviderOrganization);
        routes.MapGet("/api/admin/organizations/{id:guid}", AdminEndpointsLscc010.GetOrganizationById);
        routes.MapPut("/api/admin/organizations/{id:guid}", UpdateOrganization);
        routes.MapPatch("/api/admin/organizations/{id:guid}/provider-mode", UpdateOrganizationProviderMode);

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
        // LS-ID-TNT-012: TenantAdmin/PlatformAdmin bypass via RequirePermissionFilter;
        // StandardUsers with explicit TENANT.users:manage grant are also allowed.
        routes.MapPatch("/api/admin/users/{id:guid}/deactivate",            DeactivateUser)
            .RequirePermission(PermCodes.TenantUsersManage);

        // UIX-002: activate user
        routes.MapPost("/api/admin/users/{id:guid}/activate",               ActivateUser)
            .RequirePermission(PermCodes.TenantUsersManage);

        // UIX-002: invite user
        routes.MapPost("/api/admin/users/invite",                           InviteUser)
            .RequirePermission(PermCodes.TenantInvitationsManage);

        // UIX-002: resend invite
        routes.MapPost("/api/admin/users/{id:guid}/resend-invite",          ResendInvite)
            .RequirePermission(PermCodes.TenantInvitationsManage);

        // Admin can edit a user's primary phone number on file.
        routes.MapPatch("/api/admin/users/{id:guid}/phone",                 UpdateUserPhone)
            .RequirePermission(PermCodes.TenantUsersManage);

        // UIX-003-03: security / session admin actions
        routes.MapPost("/api/admin/users/{id:guid}/lock",                   LockUser)
            .RequirePermission(PermCodes.TenantUsersManage);
        routes.MapPost("/api/admin/users/{id:guid}/unlock",                 UnlockUser)
            .RequirePermission(PermCodes.TenantUsersManage);
        routes.MapPost("/api/admin/users/{id:guid}/reset-password",         AdminResetPassword);
        routes.MapPost("/api/admin/users/{id:guid}/set-password",           AdminSetPassword);
        routes.MapPost("/api/admin/users/{id:guid}/force-logout",           ForceLogout);
        routes.MapGet("/api/admin/users/{id:guid}/security",                GetUserSecurity);

        // UIX-004: user activity audit trail (queries local AuditLogs by EntityId)
        routes.MapGet("/api/admin/users/{id:guid}/activity",                GetUserActivity);

        // LSCC-01-003: Admin CareConnect provider provisioning
        routes.MapGet("/api/admin/users/{id:guid}/careconnect-readiness",   GetCareConnectReadiness);
        routes.MapPost("/api/admin/users/{id:guid}/provision-careconnect",  ProvisionForCareConnect);

        // ── Role assignment ───────────────────────────────────────────────────
        // LS-ID-TNT-012: role assignment gated on TENANT.roles:assign.
        routes.MapPost("/api/admin/users/{id:guid}/roles",                  AssignRole)
            .RequirePermission(PermCodes.TenantRolesAssign);
        routes.MapDelete("/api/admin/users/{id:guid}/roles/{roleId:guid}",  RevokeRole)
            .RequirePermission(PermCodes.TenantRolesAssign);

        // UIX-002-C: assignable roles with eligibility metadata
        routes.MapGet("/api/admin/users/{id:guid}/assignable-roles",        GetAssignableRoles);

        // Phase I: scoped role summary for a user (non-global scope visibility)
        routes.MapGet("/api/admin/users/{id:guid}/scoped-roles",            GetScopedRoles);

        // ── Memberships ───────────────────────────────────────────────────────
        // UIX-002: assign user to organization, set primary, remove (scaffold)
        // LS-ID-TNT-012: membership mutations gated on TENANT.users:manage.
        routes.MapPost("/api/admin/users/{id:guid}/memberships",                                   AssignMembership)
            .RequirePermission(PermCodes.TenantUsersManage);
        routes.MapPost("/api/admin/users/{id:guid}/memberships/{membershipId:guid}/set-primary",   SetPrimaryMembership)
            .RequirePermission(PermCodes.TenantUsersManage);
        routes.MapDelete("/api/admin/users/{id:guid}/memberships/{membershipId:guid}",             RemoveMembership)
            .RequirePermission(PermCodes.TenantUsersManage);


        // ── Permissions catalog ───────────────────────────────────────────────
        // UIX-002: read-only capability/permission catalog
        routes.MapGet("/api/admin/permissions",                         ListPermissions);

        // ── Role permission management (UIX-005) ──────────────────────────────
        routes.MapGet("/api/admin/roles/{id:guid}/permissions",                              GetRolePermissions);
        routes.MapPost("/api/admin/roles/{id:guid}/permissions",                             AssignRolePermission);
        routes.MapDelete("/api/admin/roles/{id:guid}/permissions/{permissionId:guid}",        RevokeRolePermission);

        // ── User effective permissions (UIX-005) ─────────────────────────────
        routes.MapGet("/api/admin/users/{id:guid}/permissions",                              GetUserEffectivePermissions);

        // ── Authorization debug (LS-COR-AUT-008) ───────────────────────────
        routes.MapGet("/api/admin/users/{id:guid}/access-debug",                             GetAccessDebug);

        // ── Permission catalog management (LS-COR-AUT-010) ────────────────────
        routes.MapGet("/api/admin/permissions/by-product/{productCode}",                     ListPermissionsByProduct);
        routes.MapPost("/api/admin/permissions",                                             CreatePermission);
        routes.MapPatch("/api/admin/permissions/{id:guid}",                                  UpdatePermission);
        routes.MapDelete("/api/admin/permissions/{id:guid}",                                 DeactivatePermission);

        // ── LS-COR-AUT-011: ABAC Policy Management ─────────────────────────
        routes.MapGet("/api/admin/policies",                                                 ListPolicies);
        routes.MapGet("/api/admin/policies/{id:guid}",                                       GetPolicy);
        routes.MapPost("/api/admin/policies",                                                CreatePolicy);
        routes.MapPatch("/api/admin/policies/{id:guid}",                                     UpdatePolicy);
        routes.MapDelete("/api/admin/policies/{id:guid}",                                    DeactivatePolicy);

        // Policy rules
        routes.MapGet("/api/admin/policies/{policyId:guid}/rules",                           ListPolicyRules);
        routes.MapPost("/api/admin/policies/{policyId:guid}/rules",                          CreatePolicyRule);
        routes.MapPatch("/api/admin/policies/{policyId:guid}/rules/{ruleId:guid}",           UpdatePolicyRule);
        routes.MapDelete("/api/admin/policies/{policyId:guid}/rules/{ruleId:guid}",          DeletePolicyRule);

        // Permission ↔ Policy mappings
        routes.MapGet("/api/admin/permission-policies",                                      ListPermissionPolicies);
        routes.MapPost("/api/admin/permission-policies",                                     CreatePermissionPolicy);
        routes.MapDelete("/api/admin/permission-policies/{id:guid}",                         DeactivatePermissionPolicy);

        // Policy evaluation debug
        routes.MapGet("/api/admin/policies/supported-fields",                                GetSupportedFields);

        // ── LS-COR-AUT-011D: Authorization Simulation ───────────────────────
        routes.MapPost("/api/admin/authorization/simulate",                                  AdminEndpointsLscc010.SimulateAuthorization);

        // ── Membership lookup (notifications fan-out) ────────────────────────
        // Internal service-to-service endpoint used by the notifications service
        // to resolve role- or org-addressed recipients to concrete users.
        routes.MapGet("/api/admin/membership-lookup", MembershipLookup);

        // ── Notifications cache invalidation status ──────────────────────────
        // Operator-facing snapshot of identity → notifications invalidation
        // counters (configured?, attempted, succeeded, failed, last failure).
        // Surfaces obvious mis-configurations (wrong BaseUrl or shared token →
        // failures climb while succeeded stays 0) without grepping logs.
        routes.MapGet("/api/admin/notifications-cache/status",
            (INotificationsCacheClientDiagnostics diagnostics) =>
                Results.Ok(diagnostics.GetSnapshot()));

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
            logoWhiteDocumentId   = t.LogoWhiteDocumentId,
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

        var code = SlugGenerator.Normalize(body.Code);
        var (slugValid, slugError) = SlugGenerator.Validate(code);
        if (!slugValid)
            return Results.BadRequest(new { error = slugError });

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
        var tenant = Tenant.Create(body.Name.Trim(), code);
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

    private static async Task<IResult> SetTenantLogoWhite(
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

        tenant.SetLogoWhite(documentId);
        await db.SaveChangesAsync(ct);

        var callerId     = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var callerEmail  = caller.FindFirstValue(ClaimTypes.Email) ?? caller.FindFirstValue("email");
        var now          = DateTimeOffset.UtcNow;

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.tenant.logo_white_set",
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
            Action      = "LogoWhiteSet",
            Description = $"Admin set white/reversed logo for tenant {id} (document {documentId}).",
            Metadata    = JsonSerializer.Serialize(new { tenantId = id, documentId }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.tenant.logo_white_set", id.ToString()),
            Tags = ["tenant", "logo", "branding"],
        });

        return Results.Ok(new { logoWhiteDocumentId = documentId });
    }

    private static async Task<IResult> ClearTenantLogoWhite(
        Guid                id,
        ClaimsPrincipal     caller,
        IdentityDbContext   db,
        IAuditEventClient   auditClient,
        CancellationToken   ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return Results.NotFound();

        tenant.ClearLogoWhite();
        await db.SaveChangesAsync(ct);

        var callerId     = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var callerEmail  = caller.FindFirstValue(ClaimTypes.Email) ?? caller.FindFirstValue("email");
        var now          = DateTimeOffset.UtcNow;

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.tenant.logo_white_cleared",
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
            Action      = "LogoWhiteCleared",
            Description = $"Admin cleared white/reversed logo for tenant {id}.",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.tenant.logo_white_cleared", id.ToString()),
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
                groupCount = db.AccessGroupMemberships.Count(am => am.UserId == u.Id && am.MembershipStatus == MembershipStatus.Active),
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

    /// <summary>
    /// Resolve role- or organization-addressed recipients to concrete users.
    /// Used by the notifications service when fanning out a Role/Org envelope.
    ///
    /// Filters (all combinations supported except "neither"):
    ///   tenantId  — required, scopes the lookup.
    ///   roleKey   — match against Role.Name (case-insensitive) via active GLOBAL
    ///               ScopedRoleAssignments.
    ///   orgId     — restrict to users with an active UserOrganizationMembership
    ///               in that organization.
    ///
    /// Returns: { items: [{ userId, email, organizationId? }] }
    /// </summary>
    private static async Task<IResult> MembershipLookup(
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        string            tenantId  = "",
        string            roleKey   = "",
        string            orgId     = "",
        CancellationToken ct        = default)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            return Results.BadRequest(new { error = "tenantId is required and must be a GUID." });

        Guid? oid = null;
        if (!string.IsNullOrWhiteSpace(orgId))
        {
            if (!Guid.TryParse(orgId, out var parsedOid))
                return Results.BadRequest(new { error = "orgId must be a GUID." });
            oid = parsedOid;
        }

        var roleKeyTrimmed = roleKey?.Trim();
        var hasRoleFilter  = !string.IsNullOrWhiteSpace(roleKeyTrimmed);
        var hasOrgFilter   = oid.HasValue;

        if (!hasRoleFilter && !hasOrgFilter)
            return Results.BadRequest(new { error = "Provide at least one of roleKey or orgId." });

        // Tenant scope: TenantAdmins may only resolve within their own tenant.
        // PlatformAdmins (and unauthenticated internal callers — gateway-trusted)
        // may resolve any tenant. Service-to-service callers (no claims) are allowed
        // because gateway/network policy fronts this endpoint.
        var callerTenantId = caller.FindFirstValue("tenant_id");
        if (callerTenantId is not null && Guid.TryParse(callerTenantId, out var callerTid))
        {
            var isPlatformAdmin = caller.IsInRole("PlatformAdmin");
            if (!isPlatformAdmin && callerTid != tid)
                return Results.Forbid();
        }

        var q = db.Users.AsNoTracking()
            .Where(u => u.TenantId == tid && u.IsActive);

        if (hasRoleFilter)
        {
            // Explicit lower-case compare so case-insensitivity is guaranteed
            // regardless of column collation (MySQL default _ci is permissive,
            // but other deployments / future migrations may differ).
            var roleKeyLower = roleKeyTrimmed!.ToLowerInvariant();
            q = q.Where(u => db.ScopedRoleAssignments.Any(s =>
                s.UserId    == u.Id    &&
                s.IsActive             &&
                s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global &&
                s.Role.Name.ToLower() == roleKeyLower));
        }

        if (hasOrgFilter)
        {
            q = q.Where(u => db.UserOrganizationMemberships.Any(m =>
                m.UserId         == u.Id     &&
                m.IsActive                   &&
                m.OrganizationId == oid!.Value));
        }

        var items = await q
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                userId         = u.Id,
                email          = u.Email,
                phone          = u.Phone,
                organizationId = oid,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount = items.Count });
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

        var groupMemberships = await (
            from am in db.AccessGroupMemberships
            join ag in db.AccessGroups on am.GroupId equals ag.Id
            where am.UserId == id && am.MembershipStatus == MembershipStatus.Active
            select new { groupId = am.GroupId, groupName = ag.Name, joinedAtUtc = am.AddedAtUtc }
        ).ToListAsync(ct);

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
            phone             = u.Phone,
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
        Guid                      id,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        IAuditEventClient         auditClient,
        INotificationsCacheClient notificationsCache,
        CancellationToken         ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return Results.NotFound();

        // UIX-003-01: TenantAdmin may only deactivate users within their own tenant.
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

        // LS-ID-TNT-005: Backend last-admin protection.
        // Block deactivation if the target user is the only remaining active TenantAdmin
        // for this tenant.  This guard applies to all callers (tenant UI, platform admin
        // tools, direct API calls, self-targeted actions) — not just the frontend path.
        var isTenantAdmin = await db.ScopedRoleAssignments
            .AnyAsync(s => s.IsActive
                        && s.UserId == user.Id
                        && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
                        && s.Role!.Name == "TenantAdmin", ct);
        if (isTenantAdmin)
        {
            var otherActiveAdmins = await CountOtherActiveTenantAdmins(db, user.Id, user.TenantId, ct);
            if (otherActiveAdmins == 0)
                return Results.UnprocessableEntity(new
                {
                    error = "This action is not allowed because the user is the last active tenant administrator.",
                    code  = "LAST_ACTIVE_ADMIN",
                });
        }

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

        // Membership state changed for this tenant — drop the notifications
        // service's cached role/org member lists so role-addressed alerts
        // immediately stop including the deactivated user.
        notificationsCache.InvalidateTenant(
            user.TenantId,
            eventType: "identity.user.deactivated",
            reason:    $"user {user.Id} deactivated");

        return Results.NoContent();
    }

    /// <summary>
    /// PATCH /api/admin/users/{id}/phone
    ///
    /// Lets a tenant admin set or clear the user's primary phone number.
    /// Body: { "phone": "+15551234567" } — pass null/empty to clear.
    /// Phones are normalised to E.164 before persisting.
    /// Returns 200 with { phone } on success (including idempotent no-ops),
    /// 400 on invalid input, 403 cross-tenant, 404 unknown user.
    /// Emits identity.admin.user_phone_updated audit event when the value
    /// actually changes.
    /// </summary>
    private static async Task<IResult> UpdateUserPhone(
        Guid               id,
        UpdatePhoneRequest body,
        ClaimsPrincipal    caller,
        IdentityDbContext  db,
        IAuditEventClient  auditClient,
        CancellationToken  ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();
        if (IsCrossTenantAccess(caller, user.TenantId)) return Results.Forbid();

        var (ok, normalised, error) = PhoneNumber.TryNormalise(body.Phone);
        // Return both `message` (consumed by the Control Center api client)
        // and `error` (consumed by the tenant portal's PhoneEditor) so the
        // same upstream payload satisfies both BFFs without translation.
        if (!ok) return Results.BadRequest(new { error, message = error });

        var before  = user.Phone;
        var changed = user.SetPhone(normalised);
        if (!changed) return Results.Ok(new { phone = user.Phone });

        await db.SaveChangesAsync(ct);

        var callerIdStr = caller.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                       ?? caller.FindFirstValue("sub");
        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.admin.user_phone_updated",
            EventCategory = EventCategory.Administrative,
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
            Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = normalised is null ? "PhoneCleared" : "PhoneUpdated",
            Description = normalised is null
                ? $"Admin cleared phone for user '{user.Email}'."
                : $"Admin updated phone for user '{user.Email}'.",
            Before         = JsonSerializer.Serialize(new { phone = before }),
            After          = JsonSerializer.Serialize(new { phone = normalised }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "identity-service", "identity.admin.user_phone_updated", user.Id.ToString()),
            Tags = ["user-management", "phone"],
        });

        return Results.Ok(new { phone = user.Phone });
    }

    private record UpdatePhoneRequest(string? Phone);

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
        Guid                      id,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        IAuditEventClient         auditClient,
        INotificationsCacheClient notificationsCache,
        CancellationToken         ct)
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

        // Membership state changed for this tenant — drop the notifications
        // service's cached role/org member lists so role-addressed alerts
        // immediately stop including the locked user.
        notificationsCache.InvalidateTenant(
            user.TenantId,
            eventType: "identity.user.locked",
            reason:    $"user {user.Id} locked");

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
        Guid                      id,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        IAuditEventClient         auditClient,
        INotificationsCacheClient notificationsCache,
        CancellationToken         ct)
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

        // Membership state changed for this tenant — drop the notifications
        // service's cached role/org member lists so role-addressed alerts
        // immediately resume including the unlocked user.
        notificationsCache.InvalidateTenant(
            user.TenantId,
            eventType: "identity.user.unlocked",
            reason:    $"user {user.Id} unlocked");

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
    /// Admin-triggers a password reset for a user. Creates a PasswordResetToken
    /// (24-hour expiry), then dispatches a reset-link email via the notifications
    /// service when configured (LS-ID-TNT-006). Falls back to the env-gated
    /// resetToken response when notifications is not set up (dev only).
    ///
    /// Any previous pending reset tokens for this user are revoked first (idempotent).
    /// Emits identity.user.password_reset_triggered audit event.
    /// </summary>
    private static async Task<IResult> AdminResetPassword(
        Guid                                  id,
        ClaimsPrincipal                       caller,
        IdentityDbContext                     db,
        IAuditEventClient                     auditClient,
        ILoggerFactory                        loggerFactory,
        IWebHostEnvironment                   env,
        IOptions<NotificationsServiceOptions> notificationsOptions,
        INotificationsEmailClient             notificationsEmail,
        CancellationToken                     ct)
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

        // LS-ID-TNT-005: Log the raw token ONLY in non-production environments.
        // In production we never expose the raw token — email delivery is future work.
        if (!env.IsProduction())
        {
            logger.LogInformation(
                "[LS-ID-TNT-005] Password reset triggered for user {UserId} ({Email}) in tenant {TenantId}. " +
                "Reset token (NON-PRODUCTION ONLY — never expose in production): {RawToken}. " +
                "Token expires at {ExpiresAt:O}.",
                user.Id, user.Email, user.TenantId, rawToken, resetToken.ExpiresAtUtc);
        }
        else
        {
            logger.LogInformation(
                "[UIX-003-03] Admin password reset triggered for user {UserId} in tenant {TenantId}. " +
                "Token expires at {ExpiresAt:O}.",
                user.Id, user.TenantId, resetToken.ExpiresAtUtc);
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

        // LS-ID-TNT-006: Attempt email delivery via notifications service when configured.
        // The reset link is built from PortalBaseUrl (co-located in NotificationsServiceOptions)
        // using the same token encoding as the self-service forgot-password BFF route.
        var portalBaseUrl = notificationsOptions.Value.PortalBaseUrl?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(portalBaseUrl))
        {
            var resetLink   = $"{portalBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName)) displayName = user.Email;

            var (emailConfigured, delivered, deliveryError) =
                await notificationsEmail.SendPasswordResetEmailAsync(user.Email, displayName, resetLink, ct);

            if (emailConfigured)
            {
                if (delivered)
                    return Results.Ok(new { message = $"Password reset email sent to {user.Email}." });

                // Real delivery failure — return 502 so the caller knows not to claim success.
                // The reset token is retained; the admin can retry.
                return Results.Json(
                    new
                    {
                        message = "Failed to deliver the password reset email. Please try again or contact your platform administrator.",
                        error   = deliveryError,
                    },
                    statusCode: 502);
            }
        }

        // Fallback: notifications not configured (BaseUrl or PortalBaseUrl missing).
        // LS-ID-TNT-005: Expose raw token in non-production so admins can complete the flow
        // without needing a working email provider during development.
        if (!env.IsProduction())
        {
            return Results.Ok(new
            {
                message    = "Password reset initiated. Use the reset token below to complete the flow (non-production only).",
                resetToken = rawToken,
            });
        }

        return Results.Ok(new { message = "Password reset initiated." });
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
        if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters." });

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

    // =========================================================================
    // PRODUCTS CATALOG
    // =========================================================================

    /// <summary>
    /// GET /api/admin/products
    ///
    /// Returns the global active product catalog. Accessible to TenantAdmins
    /// so they can reference product names when managing user and group access.
    /// </summary>
    private static async Task<IResult> ListProducts(IdentityDbContext db, CancellationToken ct)
    {
        var products = await db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new { code = p.Code, name = p.Name, description = p.Description, isActive = p.IsActive })
            .ToListAsync(ct);

        return Results.Ok(products);
    }

    // =========================================================================

    private static async Task<IResult> ListRoles(
        IdentityDbContext db,
        int page     = 1,
        int pageSize = 20)
    {
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

        var capCounts = await db.RolePermissionAssignments
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
                permissionCount = capCountMap.GetValueOrDefault(r.Id, 0),
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

        var permAssignments = await db.RolePermissionAssignments
            .Where(a => a.RoleId == id)
            .Include(a => a.Permission)
            .ThenInclude(c => c.Product)
            .OrderBy(a => a.Permission.Product.Name)
            .ThenBy(a => a.Permission.Code)
            .ToListAsync();

        var resolvedPermissions = permAssignments.Select(a => new
        {
            id          = a.PermissionId,
            key         = a.Permission.Code,
            description = a.Permission.Description ?? a.Permission.Name,
            name        = a.Permission.Name,
            productId   = a.Permission.ProductId,
            productName = a.Permission.Product.Name,
        }).ToList();

        return Results.Ok(new
        {
            id                  = r.Id,
            name                = r.Name,
            description         = r.Description ?? "",
            isSystemRole        = r.IsSystemRole,
            userCount,
            permissionCount     = permAssignments.Count,
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
        Guid                      id,
        AssignRoleRequest         body,
        IdentityDbContext         db,
        IAuditEventClient         auditClient,
        INotificationsCacheClient notificationsCache,
        HttpContext               ctx)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return Results.NotFound(new { error = $"User '{id}' not found." });

        // UIX-003-01: TenantAdmin may only assign roles within their own tenant.
        if (IsCrossTenantAccess(ctx.User, user.TenantId)) return Results.Forbid();

        var role = await db.Roles.FindAsync(body.RoleId);
        if (role is null) return Results.NotFound(new { error = $"Role '{body.RoleId}' not found." });

        // ── LS-ID-TNT-009: Platform-role guard ──────────────────────────────
        // TenantAdmins may only assign system roles that are valid at tenant level.
        // Platform-only roles (PlatformAdmin, SuperAdmin, SystemAdmin, …) can only
        // be assigned by a PlatformAdmin regardless of tenant isolation.
        if (role.IsSystemRole)
        {
            var callerIsPlatformAdmin = ctx.User.IsInRole("PlatformAdmin");
            if (!callerIsPlatformAdmin)
            {
                var tenantAssignableSystemRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "TenantAdmin", "TenantUser" };
                if (!tenantAssignableSystemRoles.Contains(role.Name))
                    return Results.BadRequest(new
                    {
                        error   = "ROLE_NOT_TENANT_ASSIGNABLE",
                        message = "This role cannot be assigned by a tenant administrator. " +
                                  "Only TenantAdmin and TenantUser roles are assignable at the tenant level.",
                    });
            }
        }

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

        // LS-COR-AUT-007: ScopedRoleAssignment restricted to GLOBAL scope only.
        // Product-scoped roles use UserRoleAssignment/GroupRoleAssignment instead.
        var scopeType = body.ScopeType ?? ScopedRoleAssignment.ScopeTypes.Global;
        if (!ScopedRoleAssignment.ScopeTypes.IsValid(scopeType))
            return Results.BadRequest(new
            {
                error = "SCOPE_TYPE_RESTRICTED",
                message = $"ScopedRoleAssignment only supports GLOBAL scope. Received: '{scopeType}'. " +
                          "Use product role assignment endpoints for product-scoped roles.",
            });

        // Conflict check: same user + same role + GLOBAL scope
        var alreadyAssigned = await db.ScopedRoleAssignments
            .AnyAsync(s =>
                s.UserId     == id &&
                s.RoleId     == body.RoleId &&
                s.IsActive   &&
                s.ScopeType  == ScopedRoleAssignment.ScopeTypes.Global);
        if (alreadyAssigned)
            return Results.Conflict(new { error = "An identical scoped role assignment already exists for this user." });

        var now = DateTime.UtcNow;

        // LS-COR-AUT-007: GLOBAL scope only — no org/product/relationship context.
        var sra = ScopedRoleAssignment.Create(
            userId:           id,
            roleId:           body.RoleId,
            scopeType:        ScopedRoleAssignment.ScopeTypes.Global,
            tenantId:         user.TenantId,
            assignedByUserId: body.AssignedByUserId);
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

        // Role membership for this tenant changed — invalidate the notifications
        // service's cache so the next role-addressed fan-out includes this user.
        notificationsCache.InvalidateTenant(
            user.TenantId,
            eventType: "identity.role.assigned",
            reason:    $"role {body.RoleId} assigned to user {id}");

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
        Guid                      id,
        Guid                      roleId,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        IAuditEventClient         auditClient,
        INotificationsCacheClient notificationsCache)
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

        // LS-ID-TNT-005: Backend last-admin protection.
        // Block removal of the TenantAdmin role if this user is the only remaining
        // active TenantAdmin for their tenant.  The user must currently be active
        // for their admin status to count toward the tenant minimum.
        if (roleName == "TenantAdmin" && user.IsActive)
        {
            var otherActiveAdmins = await CountOtherActiveTenantAdmins(db, id, user.TenantId);
            if (otherActiveAdmins == 0)
                return Results.UnprocessableEntity(new
                {
                    error = "This action is not allowed because the user is the last active tenant administrator.",
                    code  = "LAST_ACTIVE_ADMIN",
                });
        }

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

        // Role membership for this tenant changed — invalidate the notifications
        // service's cache so the next role-addressed fan-out drops this user.
        notificationsCache.InvalidateTenant(
            user.TenantId,
            eventType: "identity.role.removed",
            reason:    $"role {roleId} removed from user {id}");

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
        var scopedOrg          = ScopeCount("ORGANIZATION");
        var scopedProduct      = ScopeCount("PRODUCT");
        var scopedRelationship = ScopeCount("RELATIONSHIP");
        var scopedTenant       = ScopeCount("TENANT");

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
        Guid                      id,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        IAuditEventClient         auditClient,
        INotificationsCacheClient notificationsCache,
        CancellationToken         ct)
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

        // Membership state changed for this tenant — drop the notifications
        // service's cached role/org member lists so role-addressed alerts
        // immediately resume including the reactivated user.
        notificationsCache.InvalidateTenant(
            user.TenantId,
            eventType: "identity.user.activated",
            reason:    $"user {user.Id} activated");

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
        InviteUserRequest                     body,
        ClaimsPrincipal                       caller,
        IdentityDbContext                     db,
        IPasswordHasher                       passwordHasher,
        IAuditEventClient                     auditClient,
        IOptions<NotificationsServiceOptions> notifOptions,
        INotificationsEmailClient             emailClient,
        IWebHostEnvironment                   env,
        CancellationToken                     ct)
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

        // LS-ID-TNT-007: Send invitation email when PortalBaseUrl is configured.
        var portalBase     = notifOptions.Value.PortalBaseUrl?.TrimEnd('/');
        var activationLink = !string.IsNullOrWhiteSpace(portalBase)
            ? $"{portalBase}/accept-invite?token={Uri.EscapeDataString(rawToken)}"
            : string.Empty;

        var displayNameStr = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(activationLink))
        {
            var (emailConfigured, emailSuccess, emailError) = await emailClient.SendInviteEmailAsync(
                emailLower, displayNameStr, activationLink, ct);

            if (emailConfigured && !emailSuccess)
            {
                return Results.Problem(
                    $"User created but invitation email could not be sent: {emailError}",
                    statusCode: 502);
            }
        }

        // Non-production: return raw token so the admin can hand-deliver the link.
        if (!env.IsProduction())
        {
            Console.WriteLine($"[INVITE TOKEN — dev only] userId={user.Id} token={rawToken}");
            return Results.Created(
                $"/api/admin/users/{user.Id}",
                new { userId = user.Id, invitationId = invitation.Id, email = emailLower, inviteToken = rawToken });
        }

        return Results.Created(
            $"/api/admin/users/{user.Id}",
            new { userId = user.Id, invitationId = invitation.Id, email = emailLower });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/resend-invite
    /// Revokes all pending invitations for the user, creates a new one.
    /// </summary>
    private static async Task<IResult> ResendInvite(
        Guid                                  id,
        ClaimsPrincipal                       caller,
        IdentityDbContext                     db,
        IAuditEventClient                     auditClient,
        IOptions<NotificationsServiceOptions> notifOptions,
        INotificationsEmailClient             emailClient,
        IWebHostEnvironment                   env,
        CancellationToken                     ct)
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

        // LS-ID-TNT-007: Send invitation email when PortalBaseUrl is configured.
        var portalBase     = notifOptions.Value.PortalBaseUrl?.TrimEnd('/');
        var activationLink = !string.IsNullOrWhiteSpace(portalBase)
            ? $"{portalBase}/accept-invite?token={Uri.EscapeDataString(rawToken)}"
            : string.Empty;

        var displayNameStr = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(activationLink))
        {
            var (emailConfigured, emailSuccess, emailError) = await emailClient.SendInviteEmailAsync(
                user.Email, displayNameStr, activationLink, ct);

            if (emailConfigured && !emailSuccess)
            {
                return Results.Problem(
                    $"Invitation refreshed but email could not be sent: {emailError}",
                    statusCode: 502);
            }
        }

        // Non-production: return raw token for hand-delivery.
        if (!env.IsProduction())
        {
            Console.WriteLine($"[RESEND INVITE — dev only] userId={id} token={rawToken}");
            return Results.Ok(new { invitationId = newInvite.Id, inviteToken = rawToken });
        }

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
        Guid                      id,
        AssignMembershipRequest   body,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        INotificationsCacheClient notificationsCache,
        CancellationToken         ct)
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

        // Org membership for this tenant changed — refresh notifications so
        // org-addressed fan-out reflects the new member immediately.
        notificationsCache.InvalidateTenant(
            user.TenantId,
            eventType: "identity.membership.changed",
            reason:    $"user {id} added to organization {body.OrganizationId}");

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
        Guid                      id,
        Guid                      membershipId,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        INotificationsCacheClient notificationsCache,
        CancellationToken         ct)
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

        // Primary org changed — org-addressed fan-out keys recipients by org,
        // so refresh notifications' membership cache for this tenant.
        if (user is not null)
        {
            notificationsCache.InvalidateTenant(
                user.TenantId,
                eventType: "identity.membership.changed",
                reason:    $"primary membership for user {id} set to {membershipId}");
        }

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
        Guid                      id,
        Guid                      membershipId,
        ClaimsPrincipal           caller,
        IdentityDbContext         db,
        INotificationsCacheClient notificationsCache,
        CancellationToken         ct)
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

        // Org membership removed — refresh notifications so org-addressed
        // fan-out drops this user immediately.
        if (user is not null)
        {
            notificationsCache.InvalidateTenant(
                user.TenantId,
                eventType: "identity.membership.changed",
                reason:    $"user {id} removed from organization {membership.OrganizationId}");
        }

        return Results.NoContent();
    }

    // =========================================================================
    // UIX-002: GROUPS
    // =========================================================================


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
        var q = db.Permissions
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
                category    = c.Category,
                productId   = c.ProductId,
                productCode = c.Product.Code,
                productName = c.Product.Name,
                isActive    = c.IsActive,
                createdAtUtc = c.CreatedAtUtc,
                updatedAtUtc = c.UpdatedAtUtc,
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

        var assignments = await db.RolePermissionAssignments
            .Where(a => a.RoleId == id)
            .Include(a => a.Permission)
            .ThenInclude(c => c.Product)
            .OrderBy(a => a.Permission.Product.Name)
            .ThenBy(a => a.Permission.Code)
            .ToListAsync(ct);

        var items = assignments.Select(a => new
        {
            id               = a.PermissionId,
            code             = a.Permission.Code,
            name             = a.Permission.Name,
            description      = a.Permission.Description,
            productId        = a.Permission.ProductId,
            productName      = a.Permission.Product.Name,
            isActive         = a.Permission.IsActive,
            assignedAtUtc    = a.AssignedAtUtc,
            assignedByUserId = a.AssignedByUserId,
        }).ToList();

        return Results.Ok(new { items, totalCount = items.Count });
    }

    private record AssignRolePermissionRequest(Guid PermissionId);

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

        var permission = await db.Permissions.FirstOrDefaultAsync(c => c.Id == body.PermissionId && c.IsActive, ct);
        if (permission is null) return Results.NotFound(new { error = "Permission not found or inactive." });

        var alreadyAssigned = await db.RolePermissionAssignments
            .AnyAsync(a => a.RoleId == id && a.PermissionId == body.PermissionId, ct);

        if (alreadyAssigned)
            return Results.Ok(new { message = "Permission already assigned to role." });

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId  = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        var assignment = RolePermissionAssignment.Create(id, body.PermissionId, callerId);
        db.RolePermissionAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Role {RoleId} assigned permission {PermissionId} by {ActorId}",
            id, body.PermissionId, callerId);

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
            Description   = $"Permission '{permission.Code}' assigned to role '{role.Name}'",
            Metadata      = System.Text.Json.JsonSerializer.Serialize(new
            {
                roleId       = id,
                permissionId = body.PermissionId,
                code         = permission.Code,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(assignAuditNow, "identity-service", "role.permission.assigned", id.ToString(), body.PermissionId.ToString()),
        });

        return Results.Created(
            $"/api/admin/roles/{id}/permissions/{body.PermissionId}",
            new { roleId = id, permissionId = body.PermissionId });
    }

    /// <summary>
    /// DELETE /api/admin/roles/{id}/permissions/{capabilityId}
    ///
    /// Revokes a capability from a role. Emits a role.permission.revoked audit event.
    /// Access: PlatformAdmin only.
    /// </summary>
    private static async Task<IResult> RevokeRolePermission(
        Guid              id,
        Guid              permissionId,
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        IAuditEventClient auditClient,
        ILoggerFactory    loggerFactory,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoints.RevokeRolePermission");

        var assignment = await db.RolePermissionAssignments
            .Include(a => a.Permission)
            .Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.RoleId == id && a.PermissionId == permissionId, ct);

        if (assignment is null)
            return Results.NotFound(new { error = "Permission assignment not found." });

        if (assignment.Role.IsSystemRole && !caller.IsInRole("PlatformAdmin"))
            return Results.Json(new { error = "System roles cannot be modified. Contact the platform administrator." }, statusCode: 403);

        if (IsCrossTenantAccess(caller, assignment.Role.TenantId))
            return Results.Forbid();

        db.RolePermissionAssignments.Remove(assignment);
        await db.SaveChangesAsync(ct);

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);

        logger.LogInformation(
            "Role {RoleId} revoked permission {PermissionId} by {ActorId}",
            id, permissionId, callerIdRaw);

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
            Description   = $"Permission '{assignment.Permission.Code}' revoked from role '{assignment.Role.Name}'",
            Metadata      = System.Text.Json.JsonSerializer.Serialize(new
            {
                roleId       = id,
                permissionId,
                code         = assignment.Permission.Code,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(revokeAuditNow, "identity-service", "role.permission.revoked", id.ToString(), permissionId.ToString()),
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

        var permAssignments = await db.RolePermissionAssignments
            .Where(a => roleIds.Contains(a.RoleId))
            .Include(a => a.Permission)
            .ThenInclude(c => c.Product)
            .ToListAsync(ct);

        var permToRoles = permAssignments
            .GroupBy(a => a.PermissionId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => roleAssignments
                        .First(r => r.RoleId == a.RoleId).Role.Name)
                      .Distinct()
                      .ToList());

        var distinctPerms = permAssignments
            .GroupBy(a => a.PermissionId)
            .Select(g => g.First().Permission)
            .OrderBy(c => c.Product.Name)
            .ThenBy(c => c.Code)
            .ToList();

        var items = distinctPerms.Select(c => new
        {
            id          = c.Id,
            code        = c.Code,
            name        = c.Name,
            description = c.Description,
            productId   = c.ProductId,
            productName = c.Product.Name,
            isActive    = c.IsActive,
            sources     = permToRoles.GetValueOrDefault(c.Id, [])
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

    // ── LS-COR-AUT-008: Authorization debug endpoint ──────────────────────────

    private static async Task<IResult> GetAccessDebug(
        Guid              id,
        IdentityDbContext db,
        IEffectiveAccessService effectiveAccessService,
        ClaimsPrincipal   caller,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin") && !caller.IsInRole("TenantAdmin"))
            return Results.Forbid();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();

        if (IsCrossTenantAccess(caller, user.TenantId))
            return Results.Forbid();

        var effectiveAccess = await effectiveAccessService.GetEffectiveAccessAsync(user.TenantId, user.Id, ct);

        var groupMemberships = await db.AccessGroupMemberships
            .Where(m => m.TenantId == user.TenantId && m.UserId == user.Id && m.MembershipStatus == MembershipStatus.Active)
            .Join(db.AccessGroups,
                m => m.GroupId,
                g => g.Id,
                (m, g) => new { g.Id, g.Name, g.Status, g.ScopeType, g.ProductCode })
            .ToListAsync(ct);

        var entitlements = await db.TenantProductEntitlements
            .Where(e => e.TenantId == user.TenantId && e.Status == EntitlementStatus.Active)
            .Select(e => new { e.ProductCode, e.Status })
            .ToListAsync(ct);

        var scopedRoles = await db.ScopedRoleAssignments
            .Where(s => s.UserId == user.Id && s.IsActive)
            .Include(s => s.Role)
            .Select(s => new { s.Role.Name, s.ScopeType })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            userId = user.Id,
            tenantId = user.TenantId,
            accessVersion = user.AccessVersion,

            products = effectiveAccess.ProductSources.Select(p => new
            {
                productCode = p.ProductCode,
                source = p.Source,
                groupId = p.GroupId,
                groupName = p.GroupName,
            }),

            roles = effectiveAccess.RoleSources.Select(r => new
            {
                roleCode = r.RoleCode,
                productCode = r.ProductCode,
                source = r.Source,
                groupId = r.GroupId,
                groupName = r.GroupName,
            }),

            systemRoles = scopedRoles.Select(r => new
            {
                roleName = r.Name,
                scopeType = r.ScopeType,
            }),

            groups = groupMemberships.Select(g => new
            {
                groupId = g.Id,
                groupName = g.Name,
                status = g.Status.ToString(),
                scopeType = g.ScopeType.ToString(),
                productCode = g.ProductCode,
            }),

            entitlements = entitlements.Select(e => new
            {
                productCode = e.ProductCode,
                status = e.Status.ToString(),
            }),

            productRolesFlat = effectiveAccess.ProductRolesFlat,
            tenantRoles = effectiveAccess.TenantRoles,

            permissions = effectiveAccess.Permissions,
            permissionSources = effectiveAccess.PermissionSources.Select(p => new
            {
                permissionCode = p.PermissionCode,
                productCode = p.ProductCode,
                source = p.Source,
                viaRoleCode = p.ViaRoleCode,
                groupId = p.GroupId,
                groupName = p.GroupName,
            }),

            policies = await GetPolicyDebugForPermissions(db, effectiveAccess.Permissions, ct),
        });
    }

    private static async Task<object[]> GetPolicyDebugForPermissions(
        IdentityDbContext db,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        if (permissions.Count == 0) return [];

        var permissionPolicies = await db.PermissionPolicies
            .Where(pp => permissions.Contains(pp.PermissionCode) && pp.IsActive)
            .ToListAsync(ct);

        if (permissionPolicies.Count == 0) return [];

        var policyIds = permissionPolicies.Select(pp => pp.PolicyId).Distinct().ToList();
        var policies = await db.Policies
            .Where(p => policyIds.Contains(p.Id) && p.IsActive)
            .Include(p => p.Rules)
            .ToListAsync(ct);

        return permissionPolicies
            .GroupBy(pp => pp.PermissionCode)
            .Select(g => (object)new
            {
                permission = g.Key,
                linkedPolicies = g.Select(pp =>
                {
                    var policy = policies.FirstOrDefault(p => p.Id == pp.PolicyId);
                    return policy == null ? null : new
                    {
                        policyCode = policy.PolicyCode,
                        policyName = policy.Name,
                        priority = policy.Priority,
                        rulesCount = policy.Rules.Count,
                        rules = policy.Rules.Select(r => new
                        {
                            field = r.Field,
                            op = r.Operator.ToString(),
                            value = r.Value,
                            conditionType = r.ConditionType.ToString(),
                            logicalGroup = r.LogicalGroup.ToString(),
                        }),
                    };
                }).Where(x => x != null),
            }).ToArray();
    }

    // ── LS-COR-AUT-009: Permission catalog by product code ──────────────────

    private static async Task<IResult> ListPermissionsByProduct(
        string productCode,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin") && !caller.IsInRole("TenantAdmin"))
            return Results.Forbid();

        var capabilities = await db.Permissions
            .Where(c => c.IsActive && c.Product.Code == productCode)
            .Include(c => c.Product)
            .OrderBy(c => c.Code)
            .Select(c => new
            {
                id = c.Id,
                code = c.Code,
                name = c.Name,
                description = c.Description,
                category = c.Category,
                productCode = c.Product.Code,
                productName = c.Product.Name,
                isActive = c.IsActive,
                createdAtUtc = c.CreatedAtUtc,
                updatedAtUtc = c.UpdatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items = capabilities, totalCount = capabilities.Count });
    }

    // ── LS-COR-AUT-010: Permission catalog CRUD ─────────────────────────────

    private record CreatePermissionRequest(string Code, string Name, string? Description, string? Category, string? ProductCode = null, Guid? ProductId = null);

    private static async Task<IResult> CreatePermission(
        CreatePermissionRequest body,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        IAuditEventClient auditClient,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        Product? product = null;
        if (!string.IsNullOrWhiteSpace(body.ProductCode))
            product = await db.Products.FirstOrDefaultAsync(p => p.Code == body.ProductCode, ct);
        else if (body.ProductId.HasValue)
            product = await db.Products.FirstOrDefaultAsync(p => p.Id == body.ProductId.Value, ct);

        if (product is null)
            return Results.BadRequest(new { error = "Invalid product. Provide a valid productCode or productId." });

        if (!Permission.IsValidCode(body.Code))
            return Results.BadRequest(new { error = $"Permission code must follow naming convention 'PRODUCT.domain:action' (e.g. SYNQ_FUND.application:create). Got: '{body.Code}'." });

        var normalizedCode = body.Code.Trim();
        var exists = await db.Permissions.AnyAsync(c => c.Code == normalizedCode, ct);
        if (exists)
            return Results.Conflict(new { error = $"Permission code '{normalizedCode}' already exists." });

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        var permission = Permission.Create(product.Id, body.Code, body.Name, body.Description, body.Category, callerId);
        db.Permissions.Add(permission);
        await db.SaveChangesAsync(ct);

        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "permission.created",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Permission", Id = permission.Id.ToString() },
            Action        = "PermissionCreated",
            Description   = $"Permission '{normalizedCode}' created for product '{product.Code}'",
            After         = System.Text.Json.JsonSerializer.Serialize(new
            {
                id = permission.Id, code = normalizedCode, name = body.Name, productCode = product.Code, category = body.Category,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "permission.created", permission.Id.ToString()),
        });

        return Results.Created($"/api/admin/permissions/{permission.Id}", new
        {
            id = permission.Id, code = normalizedCode, name = permission.Name,
            description = permission.Description, category = permission.Category,
            productCode = product.Code, productName = product.Name,
            isActive = permission.IsActive, createdAtUtc = permission.CreatedAtUtc,
        });
    }

    private record UpdatePermissionRequest(string Name, string? Description, string? Category);

    private static async Task<IResult> UpdatePermission(
        Guid id,
        UpdatePermissionRequest body,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        IAuditEventClient auditClient,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var perm = await db.Permissions.Include(c => c.Product).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (perm is null)
            return Results.NotFound(new { error = "Permission not found." });

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        var before = new { name = perm.Name, description = perm.Description, category = perm.Category };
        perm.Update(body.Name, body.Description, body.Category, callerId);
        await db.SaveChangesAsync(ct);

        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "permission.updated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Permission", Id = id.ToString() },
            Action        = "PermissionUpdated",
            Description   = $"Permission '{perm.Code}' updated",
            Before        = System.Text.Json.JsonSerializer.Serialize(before),
            After         = System.Text.Json.JsonSerializer.Serialize(new { name = body.Name, description = body.Description, category = body.Category }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "permission.updated", id.ToString()),
        });

        return Results.Ok(new
        {
            id = perm.Id, code = perm.Code, name = perm.Name,
            description = perm.Description, category = perm.Category,
            productCode = perm.Product.Code, productName = perm.Product.Name,
            isActive = perm.IsActive, updatedAtUtc = perm.UpdatedAtUtc,
        });
    }

    private static async Task<IResult> DeactivatePermission(
        Guid id,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        IAuditEventClient auditClient,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var perm = await db.Permissions.Include(c => c.Product).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (perm is null)
            return Results.NotFound(new { error = "Permission not found." });

        if (!perm.IsActive)
            return Results.Ok(new { message = "Permission already deactivated." });

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        perm.Deactivate(callerId);
        await db.SaveChangesAsync(ct);

        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "permission.deactivated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = auditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Permission", Id = id.ToString() },
            Action        = "PermissionDeactivated",
            Description   = $"Permission '{perm.Code}' deactivated for product '{perm.Product.Code}'",
            Metadata      = System.Text.Json.JsonSerializer.Serialize(new { code = perm.Code, productCode = perm.Product.Code }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "permission.deactivated", id.ToString()),
        });

        return Results.NoContent();
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

    /// <summary>
    /// LS-ID-TNT-005: Count active TenantAdmin SRAs in <paramref name="tenantId"/>
    /// that belong to users OTHER than <paramref name="excludeUserId"/>.
    ///
    /// Used by DeactivateUser and RevokeRole to enforce the last-active-admin
    /// protection server-side.  A return value of 0 means the excluded user is
    /// the sole remaining active TenantAdmin for that tenant.
    /// </summary>
    private static Task<int> CountOtherActiveTenantAdmins(
        IdentityDbContext db,
        Guid              excludeUserId,
        Guid              tenantId,
        CancellationToken ct = default) =>
        (from sra  in db.ScopedRoleAssignments
         join role in db.Roles on sra.RoleId equals role.Id
         join u    in db.Users on sra.UserId equals u.Id
         where sra.IsActive
            && sra.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
            && sra.UserId != excludeUserId
            && role.Name == "TenantAdmin"
            && u.TenantId == tenantId
            && u.IsActive
         select sra.Id)
        .CountAsync(ct);

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
                id           = o.Id,
                tenantId     = o.TenantId,
                name         = o.Name,
                displayName  = o.DisplayName ?? o.Name,
                orgType      = o.OrgType,
                providerMode = o.ProviderMode,
                isActive     = o.IsActive,
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
            id           = org.Id,
            tenantId     = org.TenantId,
            name         = org.Name,
            displayName  = org.DisplayName ?? org.Name,
            orgType      = org.OrgType,
            providerMode = org.ProviderMode,
            isActive     = org.IsActive,
        });
    }

    private static async Task<IResult> UpdateOrganizationProviderMode(
        Guid              id,
        UpdateProviderModeRequest body,
        IdentityDbContext db,
        ClaimsPrincipal   caller,
        CancellationToken ct)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(body.ProviderMode) || !ProviderModes.IsValid(body.ProviderMode))
            return Results.BadRequest(new
            {
                error = new
                {
                    code    = "INVALID_PROVIDER_MODE",
                    message = $"Invalid provider mode: '{body.ProviderMode}'. Valid values: sell, manage."
                }
            });

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
            return Results.NotFound(new { error = $"Organization '{id}' not found." });

        org.SetProviderMode(body.ProviderMode);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id           = org.Id,
            providerMode = org.ProviderMode,
        });
    }

    // ── Request / response DTOs (private, scoped to AdminEndpoints) ─────────

    private record UpdateProviderModeRequest(string ProviderMode);

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

    // =========================================================================
    // LS-COR-AUT-011: ABAC POLICY MANAGEMENT
    // =========================================================================

    private static async Task<IResult> ListPolicies(
        IdentityDbContext db,
        ClaimsPrincipal caller,
        string productCode = "",
        string search = "",
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var q = db.Policies
            .Include(p => p.Rules)
            .Include(p => p.PermissionPolicies)
            .AsQueryable();

        if (activeOnly)
            q = q.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(productCode))
            q = q.Where(p => p.ProductCode == productCode);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.PolicyCode.Contains(search) || p.Name.Contains(search));

        var policies = await q
            .OrderBy(p => p.Priority).ThenBy(p => p.PolicyCode).ThenBy(p => p.Id)
            .Select(p => new
            {
                id = p.Id,
                policyCode = p.PolicyCode,
                name = p.Name,
                description = p.Description,
                productCode = p.ProductCode,
                isActive = p.IsActive,
                priority = p.Priority,
                effect = p.Effect.ToString(),
                rulesCount = p.Rules.Count,
                permissionCount = p.PermissionPolicies.Count(pp => pp.IsActive),
                createdAtUtc = p.CreatedAtUtc,
                updatedAtUtc = p.UpdatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items = policies, totalCount = policies.Count });
    }

    private static async Task<IResult> GetPolicy(
        Guid id,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var policy = await db.Policies
            .Include(p => p.Rules)
            .Include(p => p.PermissionPolicies)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (policy is null) return Results.NotFound();

        return Results.Ok(new
        {
            id = policy.Id,
            policyCode = policy.PolicyCode,
            name = policy.Name,
            description = policy.Description,
            productCode = policy.ProductCode,
            isActive = policy.IsActive,
            priority = policy.Priority,
            effect = policy.Effect.ToString(),
            createdAtUtc = policy.CreatedAtUtc,
            updatedAtUtc = policy.UpdatedAtUtc,
            createdBy = policy.CreatedBy,
            updatedBy = policy.UpdatedBy,
            rules = policy.Rules.Select(r => new
            {
                id = r.Id,
                conditionType = r.ConditionType.ToString(),
                field = r.Field,
                op = r.Operator.ToString(),
                value = r.Value,
                logicalGroup = r.LogicalGroup.ToString(),
                createdAtUtc = r.CreatedAtUtc,
            }),
            permissionMappings = policy.PermissionPolicies.Select(pp => new
            {
                id = pp.Id,
                permissionCode = pp.PermissionCode,
                isActive = pp.IsActive,
                createdAtUtc = pp.CreatedAtUtc,
            }),
        });
    }

    private record CreatePolicyRequest(
        string PolicyCode,
        string Name,
        string ProductCode,
        string? Description = null,
        int Priority = 0,
        string Effect = "Allow");

    private static async Task<IResult> CreatePolicy(
        CreatePolicyRequest body,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        IAuditEventClient auditClient,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        if (!Identity.Domain.Policy.IsValidPolicyCode(body.PolicyCode))
            return Results.BadRequest(new { error = $"Policy code must follow naming convention 'PRODUCT.domain.qualifier' (e.g. SYNQ_FUND.approval.limit). Got: '{body.PolicyCode}'." });

        if (!Enum.TryParse<Identity.Domain.PolicyEffect>(body.Effect, true, out var effect))
            return Results.BadRequest(new { error = $"Invalid effect: '{body.Effect}'. Valid: Allow, Deny" });

        var exists = await db.Policies.AnyAsync(p => p.PolicyCode == body.PolicyCode.Trim(), ct);
        if (exists)
            return Results.Conflict(new { error = $"Policy code '{body.PolicyCode}' already exists." });

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        var policy = Identity.Domain.Policy.Create(
            body.PolicyCode, body.Name, body.ProductCode,
            body.Description, body.Priority, effect, callerId);

        db.Policies.Add(policy);
        await db.SaveChangesAsync(ct);
        policyVersionProvider.Increment();

        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "policy.created",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Policy", Id = policy.Id.ToString() },
            Action        = "PolicyCreated",
            Description   = $"Policy '{policy.PolicyCode}' (effect={effect}) created for product '{body.ProductCode}'",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "policy.created", policy.Id.ToString()),
        });

        return Results.Created($"/api/admin/policies/{policy.Id}", new
        {
            id = policy.Id, policyCode = policy.PolicyCode, name = policy.Name,
            description = policy.Description, productCode = policy.ProductCode,
            isActive = policy.IsActive, priority = policy.Priority,
            effect = policy.Effect.ToString(),
            createdAtUtc = policy.CreatedAtUtc,
        });
    }

    private record UpdatePolicyRequest(string Name, string? Description, int Priority, string? Effect = null);

    private static async Task<IResult> UpdatePolicy(
        Guid id,
        UpdatePolicyRequest body,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        IAuditEventClient auditClient,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is null)
            return Results.NotFound(new { error = "Policy not found." });

        Identity.Domain.PolicyEffect? effect = null;
        if (!string.IsNullOrWhiteSpace(body.Effect))
        {
            if (!Enum.TryParse<Identity.Domain.PolicyEffect>(body.Effect, true, out var parsedEffect))
                return Results.BadRequest(new { error = $"Invalid effect: '{body.Effect}'. Valid: Allow, Deny" });
            effect = parsedEffect;
        }

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        policy.Update(body.Name, body.Description, body.Priority, effect, callerId);
        await db.SaveChangesAsync(ct);
        policyVersionProvider.Increment();

        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "policy.updated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Policy", Id = id.ToString() },
            Action        = "PolicyUpdated",
            Description   = $"Policy '{policy.PolicyCode}' updated (effect={policy.Effect})",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "policy.updated", id.ToString()),
        });

        return Results.Ok(new
        {
            id = policy.Id, policyCode = policy.PolicyCode, name = policy.Name,
            description = policy.Description, productCode = policy.ProductCode,
            isActive = policy.IsActive, priority = policy.Priority,
            effect = policy.Effect.ToString(),
            updatedAtUtc = policy.UpdatedAtUtc,
        });
    }

    private static async Task<IResult> DeactivatePolicy(
        Guid id,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        IAuditEventClient auditClient,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is null)
            return Results.NotFound(new { error = "Policy not found." });

        if (!policy.IsActive)
            return Results.Ok(new { message = "Policy already deactivated." });

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? callerId = Guid.TryParse(callerIdRaw, out var cid) ? cid : null;

        policy.Deactivate(callerId);
        await db.SaveChangesAsync(ct);
        policyVersionProvider.Increment();

        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "policy.deactivated",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = auditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Policy", Id = id.ToString() },
            Action        = "PolicyDeactivated",
            Description   = $"Policy '{policy.PolicyCode}' deactivated",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "policy.deactivated", id.ToString()),
        });

        return Results.NoContent();
    }

    // ── Policy Rules ────────────────────────────────────────────────────────────

    private static async Task<IResult> ListPolicyRules(
        Guid policyId,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var policy = await db.Policies.Include(p => p.Rules).FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy is null) return Results.NotFound();

        return Results.Ok(new
        {
            policyId = policy.Id,
            policyCode = policy.PolicyCode,
            rules = policy.Rules.Select(r => new
            {
                id = r.Id,
                conditionType = r.ConditionType.ToString(),
                field = r.Field,
                op = r.Operator.ToString(),
                value = r.Value,
                logicalGroup = r.LogicalGroup.ToString(),
                createdAtUtc = r.CreatedAtUtc,
            }),
        });
    }

    private record CreatePolicyRuleRequest(
        string ConditionType,
        string Field,
        string Operator,
        string Value,
        string LogicalGroup = "And");

    private static async Task<IResult> CreatePolicyRule(
        Guid policyId,
        CreatePolicyRuleRequest body,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy is null)
            return Results.NotFound(new { error = "Policy not found." });

        if (!Enum.TryParse<Identity.Domain.PolicyConditionType>(body.ConditionType, true, out var conditionType))
            return Results.BadRequest(new { error = $"Invalid ConditionType: '{body.ConditionType}'. Valid: {string.Join(", ", Enum.GetNames<Identity.Domain.PolicyConditionType>())}" });

        if (!Enum.TryParse<Identity.Domain.RuleOperator>(body.Operator, true, out var op))
            return Results.BadRequest(new { error = $"Invalid Operator: '{body.Operator}'. Valid: {string.Join(", ", Enum.GetNames<Identity.Domain.RuleOperator>())}" });

        if (!Enum.TryParse<Identity.Domain.LogicalGroupType>(body.LogicalGroup, true, out var logicalGroup))
            return Results.BadRequest(new { error = $"Invalid LogicalGroup: '{body.LogicalGroup}'. Valid: And, Or" });

        if (!Identity.Domain.PolicyRule.IsFieldSupported(body.Field))
            return Results.BadRequest(new { error = $"Field '{body.Field}' is not supported. Supported: {string.Join(", ", Identity.Domain.PolicyRule.GetSupportedFields())}" });

        try
        {
            var rule = Identity.Domain.PolicyRule.Create(policyId, conditionType, body.Field, op, body.Value, logicalGroup);
            db.PolicyRules.Add(rule);
            await db.SaveChangesAsync(ct);
            policyVersionProvider.Increment();

            return Results.Created($"/api/admin/policies/{policyId}/rules/{rule.Id}", new
            {
                id = rule.Id,
                policyId = rule.PolicyId,
                conditionType = rule.ConditionType.ToString(),
                field = rule.Field,
                op = rule.Operator.ToString(),
                value = rule.Value,
                logicalGroup = rule.LogicalGroup.ToString(),
                createdAtUtc = rule.CreatedAtUtc,
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private record UpdatePolicyRuleRequest(
        string ConditionType,
        string Field,
        string Operator,
        string Value,
        string LogicalGroup);

    private static async Task<IResult> UpdatePolicyRule(
        Guid policyId,
        Guid ruleId,
        UpdatePolicyRuleRequest body,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var rule = await db.PolicyRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.PolicyId == policyId, ct);
        if (rule is null)
            return Results.NotFound(new { error = "Rule not found." });

        if (!Enum.TryParse<Identity.Domain.PolicyConditionType>(body.ConditionType, true, out var conditionType))
            return Results.BadRequest(new { error = $"Invalid ConditionType: '{body.ConditionType}'." });

        if (!Enum.TryParse<Identity.Domain.RuleOperator>(body.Operator, true, out var op))
            return Results.BadRequest(new { error = $"Invalid Operator: '{body.Operator}'." });

        if (!Enum.TryParse<Identity.Domain.LogicalGroupType>(body.LogicalGroup, true, out var logicalGroup))
            return Results.BadRequest(new { error = $"Invalid LogicalGroup: '{body.LogicalGroup}'." });

        try
        {
            rule.Update(conditionType, body.Field, op, body.Value, logicalGroup);
            await db.SaveChangesAsync(ct);
            policyVersionProvider.Increment();

            return Results.Ok(new
            {
                id = rule.Id,
                policyId = rule.PolicyId,
                conditionType = rule.ConditionType.ToString(),
                field = rule.Field,
                op = rule.Operator.ToString(),
                value = rule.Value,
                logicalGroup = rule.LogicalGroup.ToString(),
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeletePolicyRule(
        Guid policyId,
        Guid ruleId,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var rule = await db.PolicyRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.PolicyId == policyId, ct);
        if (rule is null)
            return Results.NotFound(new { error = "Rule not found." });

        db.PolicyRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        policyVersionProvider.Increment();

        return Results.NoContent();
    }

    // ── Permission ↔ Policy Mappings ────────────────────────────────────────────

    private static async Task<IResult> ListPermissionPolicies(
        IdentityDbContext db,
        ClaimsPrincipal caller,
        string? permissionCode = null,
        Guid? policyId = null,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var q = db.PermissionPolicies
            .Include(pp => pp.Policy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(permissionCode))
            q = q.Where(pp => pp.PermissionCode == permissionCode);

        if (policyId.HasValue)
            q = q.Where(pp => pp.PolicyId == policyId.Value);

        var items = await q
            .Select(pp => new
            {
                id = pp.Id,
                permissionCode = pp.PermissionCode,
                policyId = pp.PolicyId,
                policyCode = pp.Policy.PolicyCode,
                policyName = pp.Policy.Name,
                isActive = pp.IsActive,
                createdAtUtc = pp.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount = items.Count });
    }

    private record CreatePermissionPolicyRequest(string PermissionCode, Guid PolicyId);

    private static async Task<IResult> CreatePermissionPolicy(
        CreatePermissionPolicyRequest body,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        IAuditEventClient auditClient,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == body.PolicyId, ct);
        if (policy is null)
            return Results.BadRequest(new { error = "Policy not found." });

        var permExists = await db.Permissions.AnyAsync(p => p.Code == body.PermissionCode && p.IsActive, ct);
        if (!permExists)
            return Results.BadRequest(new { error = $"Permission '{body.PermissionCode}' not found or inactive." });

        var existing = await db.PermissionPolicies
            .FirstOrDefaultAsync(pp => pp.PermissionCode == body.PermissionCode && pp.PolicyId == body.PolicyId, ct);

        if (existing != null)
        {
            if (existing.IsActive)
                return Results.Conflict(new { error = "This permission-policy mapping already exists." });

            existing.Activate();
            await db.SaveChangesAsync(ct);
            policyVersionProvider.Increment();
            return Results.Ok(new { id = existing.Id, permissionCode = existing.PermissionCode, policyId = existing.PolicyId, isActive = true, message = "Reactivated existing mapping." });
        }

        var mapping = Identity.Domain.PermissionPolicy.Create(body.PermissionCode, body.PolicyId);
        db.PermissionPolicies.Add(mapping);
        await db.SaveChangesAsync(ct);
        policyVersionProvider.Increment();

        var callerIdRaw = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        var auditNow = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "permission_policy.created",
            EventCategory = EventCategory.Administrative,
            SourceSystem  = "identity-service",
            SourceService = "admin-api",
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Actor         = new AuditEventActorDto { Id = callerIdRaw ?? "system", Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "PermissionPolicy", Id = mapping.Id.ToString() },
            Action        = "PermissionPolicyCreated",
            Description   = $"Permission '{body.PermissionCode}' linked to policy '{policy.PolicyCode}'",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "identity-service", "permission_policy.created", mapping.Id.ToString()),
        });

        return Results.Created($"/api/admin/permission-policies/{mapping.Id}", new
        {
            id = mapping.Id,
            permissionCode = mapping.PermissionCode,
            policyId = mapping.PolicyId,
            policyCode = policy.PolicyCode,
            isActive = mapping.IsActive,
            createdAtUtc = mapping.CreatedAtUtc,
        });
    }

    private static async Task<IResult> DeactivatePermissionPolicy(
        Guid id,
        IdentityDbContext db,
        ClaimsPrincipal caller,
        BuildingBlocks.Authorization.IPolicyVersionProvider policyVersionProvider,
        CancellationToken ct = default)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var mapping = await db.PermissionPolicies.FirstOrDefaultAsync(pp => pp.Id == id, ct);
        if (mapping is null)
            return Results.NotFound(new { error = "Permission-policy mapping not found." });

        if (!mapping.IsActive)
            return Results.Ok(new { message = "Mapping already deactivated." });

        mapping.Deactivate();
        await db.SaveChangesAsync(ct);
        policyVersionProvider.Increment();

        return Results.NoContent();
    }

    // ── Supported fields for condition builder ──────────────────────────────────

    private static IResult GetSupportedFields(ClaimsPrincipal caller)
    {
        if (!caller.IsInRole("PlatformAdmin"))
            return Results.Forbid();

        var fields = Identity.Domain.PolicyRule.GetSupportedFields();
        var operators = Enum.GetNames<Identity.Domain.RuleOperator>();
        var conditionTypes = Enum.GetNames<Identity.Domain.PolicyConditionType>();
        var logicalGroups = Enum.GetNames<Identity.Domain.LogicalGroupType>();

        var effects = Enum.GetNames<Identity.Domain.PolicyEffect>();

        return Results.Ok(new
        {
            fields = fields.ToList(),
            operators,
            conditionTypes,
            logicalGroups,
            effects,
        });
    }

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
            .Select(o => new { o.Id, o.TenantId, o.Name, o.OrgType, o.ProviderMode, o.IsActive, o.CreatedAtUtc })
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

    // =========================================================================
    // LS-COR-AUT-011D: AUTHORIZATION SIMULATION
    // =========================================================================

    internal static async Task<IResult> SimulateAuthorization(
        SimulateAuthorizationRequest body,
        ClaimsPrincipal caller,
        IAuthorizationSimulationService simulationService,
        IdentityDbContext db,
        IAuditEventClient auditClient,
        CancellationToken ct)
    {
        var isPlatformAdmin = caller.IsInRole("PlatformAdmin");
        var isTenantAdmin = caller.IsInRole("TenantAdmin");
        if (!isPlatformAdmin && !isTenantAdmin)
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(body.PermissionCode))
            return Results.BadRequest(new { error = "permissionCode is required." });

        if (body.TenantId == Guid.Empty)
            return Results.BadRequest(new { error = "tenantId is required." });

        if (body.UserId == Guid.Empty)
            return Results.BadRequest(new { error = "userId is required." });

        var permParts = body.PermissionCode.Trim().Split('.');
        if (permParts.Length < 2)
            return Results.BadRequest(new { error = "permissionCode must contain at least one dot separator (e.g. PRODUCT.resource:action)." });

        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == body.TenantId, ct);
        if (!tenantExists)
            return Results.NotFound(new { error = "Tenant not found." });

        if (!isPlatformAdmin)
        {
            var rawTid = caller.FindFirstValue("tenant_id");
            if (rawTid is null || !Guid.TryParse(rawTid, out var callerTid) || callerTid != body.TenantId)
                return Results.Forbid();
        }

        var targetUser = await db.Users
            .Where(u => u.Id == body.UserId && u.TenantId == body.TenantId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);
        if (targetUser == null)
            return Results.NotFound(new { error = "User not found in the specified tenant." });

        if (body.DraftPolicy != null)
        {
            if (string.IsNullOrWhiteSpace(body.DraftPolicy.PolicyCode))
                return Results.BadRequest(new { error = "draftPolicy.policyCode is required." });
            if (string.IsNullOrWhiteSpace(body.DraftPolicy.Name))
                return Results.BadRequest(new { error = "draftPolicy.name is required." });
            if (body.DraftPolicy.Rules != null)
            {
                foreach (var rule in body.DraftPolicy.Rules)
                {
                    if (string.IsNullOrWhiteSpace(rule.Field))
                        return Results.BadRequest(new { error = "Each draft rule must have a 'field'." });
                    if (string.IsNullOrWhiteSpace(rule.Value))
                        return Results.BadRequest(new { error = $"Draft rule for field '{rule.Field}' must have a 'value'." });
                    if (!string.IsNullOrWhiteSpace(rule.Operator) && !Enum.TryParse<Identity.Domain.RuleOperator>(rule.Operator, ignoreCase: true, out _))
                        return Results.BadRequest(new { error = $"Draft rule for field '{rule.Field}' has invalid operator '{rule.Operator}'. Valid operators: {string.Join(", ", Enum.GetNames<Identity.Domain.RuleOperator>())}." });
                    if (!string.IsNullOrWhiteSpace(rule.LogicalGroup) && !rule.LogicalGroup.Equals("And", StringComparison.OrdinalIgnoreCase) && !rule.LogicalGroup.Equals("Or", StringComparison.OrdinalIgnoreCase))
                        return Results.BadRequest(new { error = $"Draft rule for field '{rule.Field}' has invalid logicalGroup '{rule.LogicalGroup}'. Valid values: And, Or." });
                }
            }
        }

        var mode = body.DraftPolicy != null ? SimulationMode.Draft : SimulationMode.Live;

        var request = new SimulationRequest
        {
            TenantId = body.TenantId,
            UserId = body.UserId,
            PermissionCode = body.PermissionCode.Trim(),
            ResourceContext = body.ResourceContext,
            RequestContext = body.RequestContext,
            Mode = mode,
            DraftPolicy = body.DraftPolicy != null ? new DraftPolicyInput
            {
                PolicyCode = body.DraftPolicy.PolicyCode,
                Name = body.DraftPolicy.Name,
                Description = body.DraftPolicy.Description,
                Priority = body.DraftPolicy.Priority,
                Effect = body.DraftPolicy.Effect ?? "Allow",
                Rules = body.DraftPolicy.Rules?.Select(r => new DraftRuleInput
                {
                    Field = r.Field,
                    Operator = r.Operator ?? "Equals",
                    Value = r.Value,
                    LogicalGroup = r.LogicalGroup ?? "And",
                }).ToList() ?? [],
            } : null,
            ExcludePolicyIds = body.ExcludePolicyIds,
        };

        var result = await simulationService.SimulateAsync(request, ct);

        var callerIdStr = caller.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? caller.FindFirstValue("sub") ?? "unknown";

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "authorization.simulation.executed",
            EventCategory = LegalSynq.AuditClient.Enums.EventCategory.Administrative,
            Visibility    = LegalSynq.AuditClient.Enums.VisibilityScope.Platform,
            Severity      = LegalSynq.AuditClient.Enums.SeverityLevel.Info,
            SourceSystem  = "Identity",
            SourceService = "AdminEndpoints",
            Action        = "SimulateAuthorization",
            Description   = $"Admin {callerIdStr} simulated authorization for user {body.UserId} permission '{body.PermissionCode}' in tenant {body.TenantId}. Mode={mode}, Result={result.Allowed}",
            Outcome       = result.Allowed ? "allow" : "deny",
            Scope         = new LegalSynq.AuditClient.DTOs.AuditEventScopeDto { TenantId = body.TenantId.ToString() },
            Actor         = new LegalSynq.AuditClient.DTOs.AuditEventActorDto { Id = callerIdStr, Type = LegalSynq.AuditClient.Enums.ActorType.User },
            Entity        = new LegalSynq.AuditClient.DTOs.AuditEventEntityDto { Type = "AuthorizationSimulation", Id = body.UserId.ToString() },
            Metadata      = JsonSerializer.Serialize(new { permissionCode = body.PermissionCode, mode = mode.ToString(), allowed = result.Allowed }),
            IdempotencyKey = $"sim:{callerIdStr}:{body.UserId}:{body.PermissionCode}:{DateTime.UtcNow:yyyyMMddHHmmss}",
            Tags          = ["simulation", "authorization", mode.ToString().ToLowerInvariant()],
        });

        return Results.Ok(result);
    }

    internal record SimulateAuthorizationRequest
    {
        public Guid TenantId { get; init; }
        public Guid UserId { get; init; }
        public string PermissionCode { get; init; } = string.Empty;
        public Dictionary<string, object?>? ResourceContext { get; init; }
        public Dictionary<string, string>? RequestContext { get; init; }
        public SimulateAuthDraftPolicyInput? DraftPolicy { get; init; }
        public List<Guid>? ExcludePolicyIds { get; init; }
    }

    internal record SimulateAuthDraftPolicyInput
    {
        public string PolicyCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int Priority { get; init; }
        public string? Effect { get; init; }
        public List<SimulateAuthDraftRuleInput>? Rules { get; init; }
    }

    internal record SimulateAuthDraftRuleInput
    {
        public string Field { get; init; } = string.Empty;
        public string? Operator { get; init; }
        public string Value { get; init; } = string.Empty;
        public string? LogicalGroup { get; init; }
    }
}
