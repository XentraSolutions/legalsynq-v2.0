namespace Support.Api.Auth;

public static class SupportRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string SupportAdmin  = "SupportAdmin";
    public const string SupportManager = "SupportManager";
    public const string SupportAgent  = "SupportAgent";
    public const string TenantAdmin   = "TenantAdmin";
    public const string TenantUser    = "TenantUser";

    public static readonly string[] All =
    {
        PlatformAdmin, SupportAdmin, SupportManager, SupportAgent, TenantAdmin, TenantUser
    };

    public static readonly string[] InternalStaff =
    {
        PlatformAdmin, SupportAdmin, SupportManager, SupportAgent
    };

    public static readonly string[] Managers =
    {
        PlatformAdmin, SupportAdmin, SupportManager
    };
}

public static class SupportPolicies
{
    public const string SupportRead     = "SupportRead";
    public const string SupportWrite    = "SupportWrite";
    public const string SupportManage   = "SupportManage";
    public const string SupportInternal = "SupportInternal";
}
