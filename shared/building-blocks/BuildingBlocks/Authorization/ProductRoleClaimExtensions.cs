using System.Security.Claims;

namespace BuildingBlocks.Authorization;

public static class ProductRoleClaimExtensions
{
    public static IReadOnlyCollection<string> GetProductRoles(this ClaimsPrincipal principal) =>
        principal.FindAll("product_roles").Select(c => c.Value).ToList().AsReadOnly();

    public static bool HasProductAccess(this ClaimsPrincipal principal, string productCode)
    {
        if (principal.IsTenantAdminOrAbove())
            return true;

        var prefix = productCode + ":";
        return principal.FindAll("product_roles")
            .Any(c =>
            {
                var val = c.Value;
                return val.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && val.Length > prefix.Length;
            });
    }

    public static bool HasProductRole(this ClaimsPrincipal principal, string productCode, IReadOnlyList<string> requiredRoles)
    {
        if (principal.IsTenantAdminOrAbove())
            return true;

        var userProductRoles = principal.FindAll("product_roles")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var prefix = productCode + ":";
        return requiredRoles.Any(role =>
            userProductRoles.Contains(prefix + role));
    }

    public static bool IsTenantAdminOrAbove(this ClaimsPrincipal principal) =>
        principal.IsInRole(Roles.PlatformAdmin) ||
        principal.IsInRole(Roles.TenantAdmin);
}
