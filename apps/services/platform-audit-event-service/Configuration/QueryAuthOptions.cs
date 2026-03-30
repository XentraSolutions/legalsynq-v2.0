namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Authorization options for audit event query/retrieval endpoints.
/// Bound from "QueryAuth" section in appsettings.
/// Environment variable override prefix: QueryAuth__
/// </summary>
public sealed class QueryAuthOptions
{
    public const string SectionName = "QueryAuth";

    /// <summary>
    /// Auth mode for query/read endpoints.
    /// Allowed values: "None" | "ApiKey" | "Bearer"
    /// Environment variable: QueryAuth__Mode
    /// </summary>
    public string Mode { get; set; } = "None";

    /// <summary>
    /// Roles that may query events across all tenants (platform admin scope).
    /// Applied when Mode = "Bearer".
    /// </summary>
    public List<string> PlatformAdminRoles { get; set; } = ["platform-audit-admin"];

    /// <summary>
    /// Roles that may query events scoped to their own tenant only.
    /// Applied when Mode = "Bearer".
    /// </summary>
    public List<string> TenantAdminRoles { get; set; } = ["tenant-admin", "compliance-officer"];

    /// <summary>
    /// When true, a caller without a tenantId claim may only see platform-level
    /// events (TenantId IS NULL). Tenant-scoped events require matching tenantId claim.
    /// </summary>
    public bool EnforceTenantScope { get; set; } = true;

    /// <summary>
    /// Maximum number of records returnable in a single query, regardless of PageSize.
    /// Prevents unbounded reads in high-volume stores.
    /// </summary>
    public int MaxPageSize { get; set; } = 500;

    /// <summary>
    /// When true, the IntegrityHash field is included in query results.
    /// Platform admins always receive it; tenant/user roles may be restricted.
    /// </summary>
    public bool ExposeIntegrityHash { get; set; } = false;
}
