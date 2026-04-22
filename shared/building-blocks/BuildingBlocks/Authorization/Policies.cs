namespace BuildingBlocks.Authorization;

public static class Policies
{
    // Legacy policies (role-based)
    public const string AuthenticatedUser     = "AuthenticatedUser";
    public const string AdminOnly             = "AdminOnly";
    public const string PlatformOrTenantAdmin = "PlatformOrTenantAdmin";

    // LS-NOTIF-CORE-021 — service-to-service submission gate on POST /v1/notifications.
    // Accepts authenticated callers (user or service JWT) OR legacy unauthenticated
    // callers that supply a valid X-Tenant-Id header (backward-compat transition).
    public const string ServiceSubmission = "ServiceSubmission";

    // Capability-based policies (coarse product role gates — use for route groups)
    public const string CanReferCareConnect          = "CanReferCareConnect";
    public const string CanReceiveCareConnect        = "CanReceiveCareConnect";
    // CC2-INT-B06: role-based network management (not orgType-based)
    public const string CanManageCareConnectNetworks = "CanManageCareConnectNetworks";
    public const string CanSellLien           = "CanSellLien";
    public const string CanBuyLien            = "CanBuyLien";
    public const string CanReferFund          = "CanReferFund";
    public const string CanFundApplications   = "CanFundApplications";
}
