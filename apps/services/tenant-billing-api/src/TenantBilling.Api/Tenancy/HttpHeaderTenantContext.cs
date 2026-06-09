using Microsoft.AspNetCore.Http;

namespace TenantBilling.Api.Tenancy;

/// <summary>
/// HTTP header-backed <see cref="ITenantContext"/>. The tenant identifier is
/// expected to have been parsed and stashed into <c>HttpContext.Items</c> by
/// <see cref="TenantResolutionMiddleware"/>. Throws when accessed outside a
/// resolved request so that bypassing the middleware (for example by
/// forgetting to register it for a new endpoint) fails loudly.
/// </summary>
public sealed class HttpHeaderTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpHeaderTenantContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid TenantId
    {
        get
        {
            var http = _accessor.HttpContext
                ?? throw new InvalidOperationException(
                    "ITenantContext accessed outside an HTTP request scope.");

            if (!http.Items.TryGetValue(TenantResolutionMiddleware.HttpContextItemKey, out var raw)
                || raw is not Guid tenantId
                || tenantId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Tenant has not been resolved for this request. "
                    + "Ensure TenantResolutionMiddleware has run before reading ITenantContext.");
            }

            return tenantId;
        }
    }
}
