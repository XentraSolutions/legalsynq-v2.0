using System.Security.Claims;

namespace BuildingBlocks.Authorization;

public static class ProductRoleClaimExtensions
{
    private static readonly Dictionary<string, HashSet<string>> ProductToRolesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [ProductCodes.SynqCareConnect] = new(StringComparer.OrdinalIgnoreCase)
        {
            ProductRoleCodes.CareConnectReferrer,
            ProductRoleCodes.CareConnectReceiver,
            "CARECONNECT_ADMIN"
        },
        [ProductCodes.SynqFund] = new(StringComparer.OrdinalIgnoreCase)
        {
            ProductRoleCodes.SynqFundReferrer,
            ProductRoleCodes.SynqFundFunder,
            ProductRoleCodes.SynqFundApplicantPortal
        },
        [ProductCodes.SynqLiens] = new(StringComparer.OrdinalIgnoreCase)
        {
            ProductRoleCodes.SynqLienSeller,
            ProductRoleCodes.SynqLienBuyer,
            ProductRoleCodes.SynqLienHolder
        }
    };

    public static IReadOnlyCollection<string> GetProductRoles(this ClaimsPrincipal principal) =>
        principal.FindAll("product_roles").Select(c => c.Value).ToList().AsReadOnly();

    public static bool HasProductAccess(this ClaimsPrincipal principal, string productCode)
    {
        if (principal.IsInRole(Roles.PlatformAdmin))
            return true;

        var userProductRoles = principal.FindAll("product_roles").Select(c => c.Value);

        if (!ProductToRolesMap.TryGetValue(productCode, out var validRoles))
            return false;

        return userProductRoles.Any(r => validRoles.Contains(r));
    }

    public static bool HasProductRole(this ClaimsPrincipal principal, string productCode, IReadOnlyList<string> requiredRoles)
    {
        if (principal.IsInRole(Roles.PlatformAdmin))
            return true;

        if (!ProductToRolesMap.TryGetValue(productCode, out var validRolesForProduct))
            return false;

        var userProductRoles = principal.FindAll("product_roles")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredRoles.Any(r =>
            validRolesForProduct.Contains(r) && userProductRoles.Contains(r));
    }

    public static bool IsTenantAdminOrAbove(this ClaimsPrincipal principal) =>
        principal.IsInRole(Roles.PlatformAdmin) ||
        principal.IsInRole(Roles.TenantAdmin);
}
