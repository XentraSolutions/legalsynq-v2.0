namespace BuildingBlocks.Authorization;

public static class Policies
{
    // Legacy policies (role-based)
    public const string AuthenticatedUser     = "AuthenticatedUser";
    public const string AdminOnly             = "AdminOnly";
    public const string PlatformOrTenantAdmin = "PlatformOrTenantAdmin";

    // Capability-based policies (coarse product role gates — use for route groups)
    public const string CanReferCareConnect   = "CanReferCareConnect";
    public const string CanReceiveCareConnect = "CanReceiveCareConnect";
    public const string CanSellLien           = "CanSellLien";
    public const string CanBuyLien            = "CanBuyLien";
    public const string CanReferFund          = "CanReferFund";
    public const string CanFundApplications   = "CanFundApplications";
}
