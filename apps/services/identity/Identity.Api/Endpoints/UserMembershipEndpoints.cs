using Identity.Application.Interfaces;

namespace Identity.Api.Endpoints;

/// <summary>
/// BLK-ID-02 — Internal membership API endpoints.
///
/// These endpoints formalize Identity as a clean membership + access-control service.
/// They are internal-only (no public JWT auth) and secured with the provisioning token.
///
/// POST /api/internal/users/assign-tenant  — assign user to tenant + optional roles
/// POST /api/internal/users/assign-roles   — assign roles to a user (idempotent)
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
        // Updates User.TenantId and grants any provided roles.
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
