using Flow.Domain.Interfaces;

namespace Flow.Api.Services;

public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public const string TenantHeaderName = "X-Tenant-Id";
    public const string DefaultTenantId = "default";

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.Request.Headers[TenantHeaderName].FirstOrDefault();
        return string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId.Trim();
    }
}
