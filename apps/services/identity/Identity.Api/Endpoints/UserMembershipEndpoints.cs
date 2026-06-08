using Identity.Application.Interfaces;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

/// <summary>
/// BLK-ID-02 — Internal membership API endpoints.
///
/// These endpoints formalize Identity as a clean membership + access-control service.
/// They are internal-only (no public JWT auth) and secured with the provisioning token.
///
/// POST /api/internal/users/assign-tenant  — assign user to tenant + optional roles
/// POST /api/internal/users/assign-roles   — assign roles to a user (idempotent)
/// GET  /api/internal/users/portal-access  — tenant-scoped CareConnect portal access status
///
/// Auth: X-Provisioning-Token header must match TenantService:ProvisioningSecret.
///       When ProvisioningSecret is empty/unset, the check is skipped (dev mode).
/// </summary>
public static class UserMembershipEndpoints
{
    public static void MapUserMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/internal/users");

        // ── POST /api/internal/users/assign-tenant ────────────────────────────
        //
        // Assigns an existing Identity user to a tenant.
        // Writes idt_UserTenants and grants any provided roles.
        // Idempotent — safe to call even if the user is already in the target tenant.
        //
        // Request body:
        // {
        //   "userId":   "guid",
        //   "tenantId": "guid",
        //   "roles":    ["TenantAdmin"]  // optional
        // }

        group.MapPost("/assign-tenant", async (
            HttpContext            httpContext,
            AssignTenantRequest    body,
            IUserMembershipService membershipService,
            IConfiguration         configuration,
            ILoggerFactory         loggerFactory,
            CancellationToken      ct) =>
        {
            var log = loggerFactory.CreateLogger("Identity.Api.UserMembership.AssignTenant");

            if (!ValidateProvisioningToken(httpContext, configuration, log, "assign-tenant"))
                return Results.Unauthorized();

            // Validate required fields.
            if (body.UserId == Guid.Empty)
                return Results.BadRequest(new { error = "userId is required." });
            if (body.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId is required." });

            try
            {
                var result = await membershipService.AssignTenantAsync(
                    new AssignTenantCommand(
                        UserId:   body.UserId,
                        TenantId: body.TenantId,
                        Roles:    body.Roles ?? []),
                    ct);

                return Results.Ok(new
                {
                    userId          = result.UserId,
                    tenantId        = result.TenantId,
                    alreadyInTenant = result.AlreadyInTenant,
                    assignedRoles   = result.AssignedRoleAssignmentIds,
                });
            }
            catch (InvalidOperationException ex)
            {
                log.LogWarning(ex, "[UserMembership] AssignTenant failed for user {UserId}", body.UserId);
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // ── POST /api/internal/users/assign-roles ─────────────────────────────
        //
        // Assigns named roles to a user within a tenant scope.
        // Uses ScopedRoleAssignment (GLOBAL scope — Phase G authoritative model).
        // Idempotent — already-active assignments are skipped, not duplicated.
        //
        // Request body:
        // {
        //   "userId":   "guid",
        //   "tenantId": "guid",
        //   "roles":    ["TenantAdmin"]
        // }

        group.MapPost("/assign-roles", async (
            HttpContext            httpContext,
            AssignRolesRequest     body,
            IUserMembershipService membershipService,
            IConfiguration         configuration,
            ILoggerFactory         loggerFactory,
            CancellationToken      ct) =>
        {
            var log = loggerFactory.CreateLogger("Identity.Api.UserMembership.AssignRoles");

            if (!ValidateProvisioningToken(httpContext, configuration, log, "assign-roles"))
                return Results.Unauthorized();

            // Validate required fields.
            if (body.UserId == Guid.Empty)
                return Results.BadRequest(new { error = "userId is required." });
            if (body.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId is required." });
            if (body.Roles is not { Count: > 0 })
                return Results.BadRequest(new { error = "roles must be a non-empty array." });

            try
            {
                var result = await membershipService.AssignRolesAsync(
                    new AssignRolesCommand(
                        UserId:   body.UserId,
                        TenantId: body.TenantId,
                        Roles:    body.Roles),
                    ct);

                return Results.Ok(new
                {
                    userId            = result.UserId,
                    tenantId          = result.TenantId,
                    assignedRoles     = result.AssignedRoles,
                    skippedDuplicates = result.SkippedDuplicates,
                });
            }
            catch (InvalidOperationException ex)
            {
                log.LogWarning(ex, "[UserMembership] AssignRoles failed for user {UserId}", body.UserId);
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // ── GET /api/internal/users/portal-access?tenantId=xxx&email=xxx ─────
        //
        // CC-PORTAL-CHECK: Returns the tenant-scoped CareConnect referrer status
        // for the given email:
        //   - active_in_tenant
        //   - existing_user_other_tenant
        //   - no_account
        //
        // Always HTTP 200. Never discloses user existence alone beyond the referrer
        // access state needed by the post-referral-submit UX.
        //
        // Auth: X-Provisioning-Token (same pattern as assign-tenant / assign-roles).

        group.MapGet("/portal-access", async (
            HttpContext       httpContext,
            Guid?             tenantId,
            string?           email,
            IdentityDbContext db,
            IConfiguration    configuration,
            ILoggerFactory    loggerFactory,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("Identity.Api.UserMembership.PortalAccess");

            if (!ValidateProvisioningToken(httpContext, configuration, log, "portal-access"))
                return Results.Unauthorized();

            if (tenantId is null || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(email))
                return Results.Ok(new { status = "no_account" });

            var emailNorm = email.Trim().ToLowerInvariant();
            var emailUser = await db.Users
                .AsNoTracking()
                .Where(u => u.Email.Trim().ToLower() == emailNorm)
                .Select(u => new { u.Id, u.IsActive, u.PasswordHash })
                .FirstOrDefaultAsync(ct);

            if (emailUser is null)
                return Results.Ok(new { status = "no_account" });

            var hasTenantMembership = await db.UserTenants
                .AsNoTracking()
                .AnyAsync(ut => ut.UserId == emailUser.Id
                             && ut.TenantId == tenantId.Value
                             && ut.IsActive,
                    ct);

            var hasTenantLawFirmMembership = await db.UserOrganizationMemberships
                .AsNoTracking()
                .Where(m => m.UserId == emailUser.Id && m.IsActive)
                .Join(
                    db.Organizations.AsNoTracking().Where(o => (o.TenantId == tenantId.Value || o.TenantId == null)
                                                             && o.OrgType == "LAW_FIRM"
                                                             && o.IsActive),
                    m => m.OrganizationId,
                    o => o.Id,
                    (_, _) => true)
                .AnyAsync(ct);

            if (emailUser.IsActive
                && emailUser.PasswordHash != string.Empty
                && hasTenantMembership
                && hasTenantLawFirmMembership)
            {
                return Results.Ok(new { status = "active_in_tenant" });
            }

            return Results.Ok(new
            {
                status = hasTenantMembership
                    ? "no_account"
                    : "existing_user_other_tenant"
            });
        });

        // ── GET /api/internal/users/is-owner?tenantId=xxx&email=xxx ──────────
        //
        // CC-OWNER-CHECK: Returns { isTenantOwner: bool } — whether the given email
        // belongs to the tenant owner of the specified tenant.
        // Used by CareConnect to block tenant owners from submitting referrals.
        //
        // Always HTTP 200; never discloses existence of user alone.
        // Auth: X-Provisioning-Token (same pattern as other internal endpoints).

        group.MapGet("/is-owner", async (
            HttpContext       httpContext,
            Guid?             tenantId,
            string?           email,
            IdentityDbContext db,
            IConfiguration    configuration,
            ILoggerFactory    loggerFactory,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("Identity.Api.UserMembership.IsOwner");

            if (!ValidateProvisioningToken(httpContext, configuration, log, "is-owner"))
                return Results.Unauthorized();

            if (tenantId is null || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(email))
                return Results.Ok(new { isTenantOwner = false });

            var emailNorm = email.Trim().ToLowerInvariant();

            var ownerUserId = await db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId.Value)
                .Select(t => t.OwnerUserId)
                .FirstOrDefaultAsync(ct);

            if (ownerUserId is null)
                return Results.Ok(new { isTenantOwner = false });

            var isOwner = await db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == ownerUserId.Value && u.Email == emailNorm, ct);

            return Results.Ok(new { isTenantOwner = isOwner });
        });

        // ── GET /api/internal/users/is-any-owner?email=xxx ────────────────────
        //
        // CC-OWNER-CHECK: Returns { isTenantOwner: bool } — whether the given email
        // belongs to the owner of ANY tenant in the platform.
        // Used by CareConnect to block all tenant owners from submitting referrals
        // or self-enrolling regardless of which tenant's network they are acting on.
        //
        // Always HTTP 200; never discloses existence of user alone.
        // Auth: X-Provisioning-Token (same pattern as other internal endpoints).

        group.MapGet("/is-any-owner", async (
            HttpContext       httpContext,
            string?           email,
            IdentityDbContext db,
            IConfiguration    configuration,
            ILoggerFactory    loggerFactory,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("Identity.Api.UserMembership.IsAnyOwner");

            if (!ValidateProvisioningToken(httpContext, configuration, log, "is-any-owner"))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(email))
                return Results.Ok(new { isTenantOwner = false });

            var emailNorm = email.Trim().ToLowerInvariant();

            // Single join query — avoids two round-trips (user lookup + tenant check).
            var isOwner = await db.Users
                .AsNoTracking()
                .Where(u => u.Email == emailNorm)
                .Join(db.Tenants, u => u.Id, t => t.OwnerUserId, (u, t) => u.Id)
                .AnyAsync(ct);

            return Results.Ok(new { isTenantOwner = isOwner });
        });
    }

    // ── Shared token guard ────────────────────────────────────────────────────

    private static bool ValidateProvisioningToken(
        HttpContext    httpContext,
        IConfiguration configuration,
        ILogger        log,
        string         operation)
    {
        var secret        = configuration["TenantService:ProvisioningSecret"];
        var incomingToken = httpContext.Request.Headers["X-Provisioning-Token"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(secret))
            return true;   // dev mode — skip check

        if (!string.Equals(incomingToken, secret, StringComparison.Ordinal))
        {
            log.LogWarning(
                "[UserMembership] {Operation}: rejected — invalid X-Provisioning-Token from {RemoteIp}",
                operation, httpContext.Connection.RemoteIpAddress);
            return false;
        }

        return true;
    }

    // ── Request DTOs ──────────────────────────────────────────────────────────

    private record AssignTenantRequest(
        Guid         UserId,
        Guid         TenantId,
        List<string>? Roles = null);

    private record AssignRolesRequest(
        Guid         UserId,
        Guid         TenantId,
        List<string> Roles);
}
