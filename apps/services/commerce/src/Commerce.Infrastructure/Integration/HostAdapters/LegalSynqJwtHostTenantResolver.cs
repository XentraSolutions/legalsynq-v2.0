using System.Security.Claims;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// LS-INT-01 — LegalSynq JWT-backed <see cref="IHostTenantResolver"/>.
///
/// Extracts the <c>tenant_id</c> claim from the current JWT principal and
/// uses it as the <c>externalTenantId</c> when delegating to the EF-backed
/// <see cref="NoopHostTenantResolver"/> (which resolves against
/// <c>BillingAccountExternalRef</c> rows seeded in COM-B03).
///
/// Resolution hierarchy:
/// <list type="number">
///   <item>JWT <c>tenant_id</c> claim present → use as ExternalTenantId with HostPlatformKey = "legalsynq"</item>
///   <item>No JWT / no claim → delegate to base resolver with caller-supplied parameters (passthrough)</item>
/// </list>
///
/// Returns <c>null</c> gracefully when no <c>BillingAccountExternalRef</c> mapping
/// exists for the JWT tenant. Commerce continues in anonymous/standalone mode.
/// </summary>
internal sealed class LegalSynqJwtHostTenantResolver : IHostTenantResolver
{
    private readonly NoopHostTenantResolver _baseResolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<LegalSynqIdentityOptions> _options;

    public LegalSynqJwtHostTenantResolver(
        NoopHostTenantResolver baseResolver,
        IHttpContextAccessor httpContextAccessor,
        IOptions<LegalSynqIdentityOptions> options)
    {
        _baseResolver = baseResolver;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    public Task<Guid?> ResolveBillingAccountIdAsync(
        string hostPlatformKey,
        string externalTenantId,
        CancellationToken ct)
    {
        var jwtTenantId = GetJwtTenantId();

        if (jwtTenantId is not null)
        {
            return _baseResolver.ResolveBillingAccountIdAsync(
                _options.Value.HostPlatformKey,
                jwtTenantId,
                ct);
        }

        return _baseResolver.ResolveBillingAccountIdAsync(hostPlatformKey, externalTenantId, ct);
    }

    public Task<HostTenantRef?> ResolveByBillingAccountAsync(
        Guid billingAccountId,
        CancellationToken ct)
        => _baseResolver.ResolveByBillingAccountAsync(billingAccountId, ct);

    private string? GetJwtTenantId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null || !principal.Identity?.IsAuthenticated == true)
            return null;
        return principal.FindFirstValue("tenant_id");
    }
}
