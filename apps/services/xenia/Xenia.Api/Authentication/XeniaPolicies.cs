namespace Xenia.Api.Authentication;

public static class XeniaPolicies
{
    public const string AuthenticatedUser = "XeniaAuthenticatedUser";
    public const string PlatformAdmin = "XeniaPlatformAdmin";
    public const string TenantAdminOrAbove = "XeniaTenantAdminOrAbove";
    public const string InternalService = "XeniaInternalService";
}
