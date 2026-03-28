using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Context;

public class CurrentRequestContext : ICurrentRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentRequestContext(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue("sub"), out var uid) ? uid : null;

    public Guid? TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenant_id"), out var tid) ? tid : null;

    public string? TenantCode => User?.FindFirstValue("tenant_code");

    public string? Email => User?.FindFirstValue("email");

    public Guid? OrgId =>
        Guid.TryParse(User?.FindFirstValue("org_id"), out var oid) ? oid : null;

    public string? OrgType => User?.FindFirstValue("org_type");

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList().AsReadOnly()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public IReadOnlyCollection<string> ProductRoles =>
        User?.FindAll("product_roles").Select(c => c.Value).ToList().AsReadOnly()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public bool IsPlatformAdmin =>
        Roles.Contains(Authorization.Roles.PlatformAdmin, StringComparer.OrdinalIgnoreCase);
}
