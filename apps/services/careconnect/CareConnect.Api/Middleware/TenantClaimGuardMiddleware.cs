using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CareConnect.Api.Middleware;

/// <summary>
/// BLK-SEC-03: Tenant claim guard middleware.
///
/// For every request that passes authentication, this middleware verifies that the JWT
/// contains a valid <c>tenant_id</c> claim (a parseable GUID). If absent or malformed,
/// the request is rejected with 403 Forbidden before any handler runs.
///
/// This is an API-layer backstop that operates independently of the service/repository
/// layer: even if a future handler forgets to check <c>ctx.TenantId</c>, or a service-
/// layer bug skips the tenant filter, cross-tenant data cannot be reached because the
/// request is rejected here.
///
/// Skipped conditions:
///   - Endpoints decorated with <c>[AllowAnonymous]</c> (public endpoints use their own
///     trust-boundary validation via X-Tenant-Id + HMAC-SHA256 signature).
///   - Unauthenticated requests (handled by <c>UseAuthorization</c> → 401).
///   - PlatformAdmin — operates across tenants by design and must not be constrained
///     to a single tenant scope.
/// </summary>
public class TenantClaimGuardMiddleware
{
    private readonly RequestDelegate                       _next;
    private readonly ILogger<TenantClaimGuardMiddleware>   _logger;

    public TenantClaimGuardMiddleware(
        RequestDelegate                       next,
        ILogger<TenantClaimGuardMiddleware>   logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        // Skip anonymous endpoints — they use their own trust-boundary validation.
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        // Skip unauthenticated requests — UseAuthorization will return 401.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // PlatformAdmin operates across all tenants — no tenant_id required.
        if (context.User.IsInRole(Roles.PlatformAdmin))
        {
            await _next(context);
            return;
        }

        // Service tokens are platform-scoped M2M tokens — they never carry tenant_id
        // by design. Skip the check so internal service-to-service calls (e.g. provisioning)
        // are not rejected.
        if (context.User.IsInRole(ServiceTokenAuthenticationDefaults.ServiceRole))
        {
            await _next(context);
            return;
        }

        // Every other authenticated request must carry a valid tenant_id GUID claim.
        var tenantIdRaw = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdRaw) || !Guid.TryParse(tenantIdRaw, out _))
        {
            _logger.LogWarning(
                "TenantClaimGuard: request rejected — authenticated user lacks a valid tenant_id claim. " +
                "Path={Path} Sub={Sub}",
                context.Request.Path,
                context.User.FindFirst("sub")?.Value ?? "unknown");

            context.Response.StatusCode  = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type   = "https://httpstatuses.com/403",
                title  = "Forbidden",
                status = 403,
                detail = "A valid tenant context is required to access this resource.",
            });
            return;
        }

        await _next(context);
    }
}
