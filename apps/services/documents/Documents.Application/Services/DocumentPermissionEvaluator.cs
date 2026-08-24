using Documents.Domain.ValueObjects;

namespace Documents.Application.Services;

public static class DocumentPermissionEvaluator
{
    private static readonly Dictionary<string, string[]> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DocReader"] = new[] { "read" },
        ["DocUploader"] = new[] { "read", "write" },
        ["DocManager"] = new[] { "read", "write", "delete" },
        ["TenantAdmin"] = new[] { "read", "write", "delete" },
        ["PlatformAdmin"] = new[] { "read", "write", "delete", "admin" },
        ["service"] = new[] { "read", "write", "delete" },
    };

    private static readonly Dictionary<string, string[]> ProductRolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYNQ_LIENS:SYNQLIEN_SELLER"] = new[] { "read", "write" },
    };

    public static bool HasPermission(Principal principal, string action) =>
        principal.Roles.Any(role =>
            RolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(action)) ||
        principal.ProductRoles.Any(role =>
            ProductRolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(action));
}
