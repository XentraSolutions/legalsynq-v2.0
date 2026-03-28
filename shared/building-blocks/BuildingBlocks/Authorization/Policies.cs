namespace BuildingBlocks.Authorization;

public static class Policies
{
    public const string AuthenticatedUser = "AuthenticatedUser";
    public const string AdminOnly = "AdminOnly";
    public const string PlatformOrTenantAdmin = "PlatformOrTenantAdmin";
}
