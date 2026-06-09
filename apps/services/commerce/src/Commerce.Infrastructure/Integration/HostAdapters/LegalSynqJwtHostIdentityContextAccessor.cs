using System.Security.Claims;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// LS-INT-01 — LegalSynq JWT-backed <see cref="IHostIdentityContextAccessor"/>.
///
/// Reads the validated <see cref="ClaimsPrincipal"/> from the current HTTP
/// context and maps LegalSynq JWT claims into a <see cref="HostIdentityContext"/>.
///
/// Claim mapping:
/// <list type="bullet">
///   <item><c>sub</c> → <see cref="HostIdentityContext.ExternalUserId"/> and <see cref="HostIdentityContext.Subject"/></item>
///   <item><c>tenant_id</c> → <see cref="HostIdentityContext.ExternalTenantId"/></item>
///   <item><c>role</c> (multiple) → <see cref="HostIdentityContext.Roles"/></item>
///   <item><c>product_codes</c> (multiple) → <see cref="HostIdentityContext.Scopes"/></item>
/// </list>
///
/// Falls back to an anonymous context when the request has no authenticated
/// principal so that downstream Commerce code degrades gracefully rather than
/// throwing. This preserves standalone and staged-migration compatibility.
/// </summary>
internal sealed class LegalSynqJwtHostIdentityContextAccessor : IHostIdentityContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<LegalSynqIdentityOptions> _options;

    public LegalSynqJwtHostIdentityContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        IOptions<LegalSynqIdentityOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    public HostIdentityContext Current
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var principal = httpContext?.User;
            var hostPlatformKey = _options.Value.HostPlatformKey;

            if (principal is null || !(principal.Identity?.IsAuthenticated == true))
                return HostIdentityContext.Anonymous(hostPlatformKey);

            // "sub" claim — LegalSynq Identity emits it as JwtRegisteredClaimNames.Sub
            var sub = principal.FindFirstValue("sub")
                   ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            var tenantId = principal.FindFirstValue("tenant_id");

            var roles = principal.FindAll("role")
                .Concat(principal.FindAll(ClaimTypes.Role))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var scopes = principal.FindAll("product_codes")
                .Select(c => c.Value)
                .ToList();

            return new HostIdentityContext(
                HostPlatformKey: hostPlatformKey,
                ExternalTenantId: tenantId,
                ExternalUserId: sub,
                Subject: sub,
                Roles: roles,
                Scopes: scopes,
                IsAuthenticated: true,
                MetadataJson: null);
        }
    }
}
