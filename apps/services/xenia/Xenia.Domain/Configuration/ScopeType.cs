namespace Xenia.Domain.Configuration;

/// <summary>
/// Defines the scope of a Xenia configuration entry.
/// Configuration is resolved in ascending precedence order.
/// </summary>
public enum ScopeType
{
    /// <summary>Default application-level configuration.</summary>
    Global,

    /// <summary>Configuration scoped to a specific tenant.</summary>
    Tenant,

    /// <summary>Configuration scoped to a specific module (all tenants).</summary>
    Module,

    /// <summary>Configuration scoped to a specific tenant AND module (highest precedence).</summary>
    TenantModule,
}
