using System.Security.Claims;
using Billing.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Billing.Api.LegalSynq;

/// <summary>
/// LS-INT-01 — LegalSynq JWT-aware <see cref="ITenantIdentityContextResolver"/>.
///
/// Resolution priority:
/// <list type="number">
///   <item>
///     JWT authenticated + <c>tenant_id</c> claim present + <see cref="LegalSynqTenantContextOptions.PreferJwtTenant"/>:
///     parse claim as Guid, return <see cref="TenantResolutionSource.JwtClaim"/>.
///   </item>
///   <item>
///     JWT authenticated + role = <c>InternalService</c> + <c>X-Tenant-Id</c> header present
///     + <see cref="LegalSynqTenantContextOptions.AllowInternalTokenFallback"/>:
///     parse header, return <see cref="TenantResolutionSource.Header"/>.
///     Enables Commerce → Tenant Billing service-to-service calls with a service JWT.
///   </item>
///   <item>
///     <c>X-Tenant-Id</c> header present + <see cref="LegalSynqTenantContextOptions.AllowHeaderFallback"/>:
///     parse header, return <see cref="TenantResolutionSource.Header"/>.
///     Preserves standalone / <c>X-Internal-Token</c> mode exactly.
///   </item>
/// </list>
///
/// Returns <see cref="TenantResolutionResult.Failed"/> when no source resolves.
/// The calling middleware is responsible for writing the 400 response.
/// </summary>
internal sealed class LegalSynqJwtTenantContextResolver : ITenantIdentityContextResolver
{
    private readonly IOptions<LegalSynqTenantContextOptions> _opts;
    private readonly ILogger<LegalSynqJwtTenantContextResolver> _logger;

    public LegalSynqJwtTenantContextResolver(
        IOptions<LegalSynqTenantContextOptions> opts,
        ILogger<LegalSynqJwtTenantContextResolver> logger)
    {
        _opts = opts;
        _logger = logger;
    }

    public Task<TenantResolutionResult> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        var opts = _opts.Value;
        var principal = context.User;
        var isAuthenticated = principal?.Identity?.IsAuthenticated == true;

        // ── Priority 1: JWT tenant_id claim ──────────────────────────────
        if (isAuthenticated && opts.PreferJwtTenant)
        {
            var tenantIdClaim = principal!.FindFirstValue("tenant_id");
            if (!string.IsNullOrWhiteSpace(tenantIdClaim))
            {
                if (Guid.TryParse(tenantIdClaim, out var jwtTenantId) && jwtTenantId != Guid.Empty)
                {
                    _logger.LogDebug(
                        "Tenant resolved from JWT claim tenant_id={TenantId}",
                        jwtTenantId);
                    return Task.FromResult(
                        TenantResolutionResult.Resolved(jwtTenantId, TenantResolutionSource.JwtClaim));
                }

                _logger.LogWarning(
                    "JWT tenant_id claim present but not a valid non-empty Guid: {Value}",
                    tenantIdClaim);
            }
        }

        // ── Priority 2: Internal-service JWT + X-Tenant-Id header ────────
        if (isAuthenticated && opts.AllowInternalTokenFallback)
        {
            var roles = principal!.FindAll("role").Select(c => c.Value);
            var isInternalService = roles.Contains(
                LegalSynqBillingRoles.InternalService,
                StringComparer.OrdinalIgnoreCase);

            if (isInternalService)
            {
                var headerResult = TryParseHeader(context);
                if (headerResult.IsResolved)
                {
                    _logger.LogDebug(
                        "Tenant resolved from X-Tenant-Id header for internal-service JWT. TenantId={TenantId}",
                        headerResult.TenantId);
                    return Task.FromResult(headerResult);
                }
            }
        }

        // ── Priority 3: X-Tenant-Id header fallback ─────────────────────
        if (opts.AllowHeaderFallback)
        {
            var headerResult = TryParseHeader(context);
            if (headerResult.IsResolved)
            {
                _logger.LogDebug(
                    "Tenant resolved from X-Tenant-Id header (fallback). TenantId={TenantId}",
                    headerResult.TenantId);
                return Task.FromResult(headerResult);
            }
        }

        _logger.LogDebug(
            "Tenant resolution failed: no JWT tenant_id claim and no valid X-Tenant-Id header. " +
            "IsAuthenticated={IsAuthenticated} AllowHeaderFallback={AllowHeaderFallback}",
            isAuthenticated, opts.AllowHeaderFallback);

        return Task.FromResult(
            TenantResolutionResult.Failed(
                "Tenant could not be resolved. Provide a valid LegalSynq JWT with a tenant_id claim " +
                "or supply the X-Tenant-Id header."));
    }

    private static TenantResolutionResult TryParseHeader(HttpContext context)
    {
        var headerValue = context.Request.Headers[TenantResolutionMiddleware.HeaderName]
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(headerValue))
            return TenantResolutionResult.Failed("X-Tenant-Id header is missing.");

        if (!Guid.TryParse(headerValue, out var tenantId) || tenantId == Guid.Empty)
            return TenantResolutionResult.Failed(
                $"X-Tenant-Id header value '{headerValue}' is not a valid non-empty Guid.");

        return TenantResolutionResult.Resolved(tenantId, TenantResolutionSource.Header);
    }
}
