using System.Security.Claims;

namespace Notifications.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Paths that bypass tenant resolution entirely
        if (context.Request.Path.StartsWithSegments("/health")              ||
            context.Request.Path.StartsWithSegments("/info")                ||
            context.Request.Path.StartsWithSegments("/v1/webhooks")         ||
            context.Request.Path.StartsWithSegments("/v1/providers/catalog"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            await _next(context);
            return;
        }

        // Admin cross-tenant endpoints — no per-request tenant binding required;
        // each handler reads the optional tenantId query param itself.
        if (context.Request.Path.StartsWithSegments("/v1/admin"))
        {
            await _next(context);
            return;
        }

        // ── Authenticated requests — derive tenant from JWT claims ────────────
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // Platform administrators operate without a tenant scope.
            // Their JWT carries the "PlatformAdmin" role but no tenant_id claim.
            if (context.User.IsInRole("PlatformAdmin"))
            {
                await _next(context);
                return;
            }

            var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var claimTenantId))
            {
                _logger.LogWarning(
                    "Authenticated request is missing a valid tenant_id claim. Path={Path}",
                    context.Request.Path);

                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid tenant_id claim in token" });
                return;
            }

            context.Items["TenantId"] = claimTenantId;
            await _next(context);
            return;
        }

        // ── Unauthenticated / legacy-service requests — header fallback ──────
        // LS-NOTIF-CORE-021: the ServiceSubmission policy on POST /v1/notifications
        // already checked and succeeded for legacy requests that carry a valid
        // X-Tenant-Id header.  Log a structured warning here so log dashboards
        // can track remaining un-migrated callers.
        var tenantIdHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(tenantIdHeader) || !Guid.TryParse(tenantIdHeader, out var tenantId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid X-Tenant-Id header" });
            return;
        }

        _logger.LogWarning(
            "[LEGACY SUBMISSION] Unauthenticated request resolved tenant from X-Tenant-Id header. " +
            "TenantId={TenantId} Path={Path} RemoteIp={RemoteIp}. " +
            "Migrate caller to service-token authentication (LS-NOTIF-CORE-021).",
            tenantId,
            context.Request.Path,
            context.Connection.RemoteIpAddress?.ToString());

        context.Items["TenantId"] = tenantId;
        await _next(context);
    }
}

public static class TenantMiddlewareExtensions
{
    /// <summary>
    /// Returns the TenantId resolved by <see cref="TenantMiddleware"/>.
    /// For authenticated requests this comes from the JWT <c>tenant_id</c> claim;
    /// for unauthenticated (internal) requests it comes from the
    /// <c>X-Tenant-Id</c> header.
    /// </summary>
    public static Guid GetTenantId(this HttpContext context)
    {
        if (context.Items.TryGetValue("TenantId", out var tenantId) && tenantId is Guid id)
            return id;
        throw new InvalidOperationException("TenantId not found in request context");
    }

    /// <summary>
    /// Returns the TenantId if present, or <c>null</c> for platform-scoped
    /// requests where no tenant is in context (e.g. platform admin calls).
    /// </summary>
    public static Guid? TryGetTenantId(this HttpContext context)
    {
        if (context.Items.TryGetValue("TenantId", out var tenantId) && tenantId is Guid id)
            return id;
        return null;
    }
}
