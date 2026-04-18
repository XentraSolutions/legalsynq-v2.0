namespace Liens.Domain.Enums;

public static class WorkflowUpdateSources
{
    public const string TenantProductSettings = "TENANT_PRODUCT_SETTINGS";
    public const string ControlCenter         = "CONTROL_CENTER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        TenantProductSettings, ControlCenter
    };
}
