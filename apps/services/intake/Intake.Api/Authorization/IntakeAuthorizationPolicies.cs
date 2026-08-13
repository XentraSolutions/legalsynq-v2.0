using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Intake.Api.Authorization;

public static class IntakeAuthorizationPolicies
{
    public const string ConfigurationRead = "IntakeConfigurationRead";
    public const string ConfigurationManage = "IntakeConfigurationManage";

    public static void AddTo(AuthorizationOptions options)
    {
        options.AddPolicy(ConfigurationRead, policy =>
            policy.RequireAuthenticatedUser());

        options.AddPolicy(ConfigurationManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole(Roles.PlatformAdmin) ||
                context.User.IsInRole(Roles.TenantAdmin) ||
                HasIntakeManagementClaim(context.User)));
    }

    private static bool HasIntakeManagementClaim(ClaimsPrincipal principal) =>
        principal.Claims.Any(claim =>
            (string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase)) &&
            claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(value => string.Equals(
                    value,
                    "intake.configuration.manage",
                    StringComparison.OrdinalIgnoreCase)));
}