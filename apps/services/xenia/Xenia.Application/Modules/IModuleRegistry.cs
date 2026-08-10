namespace Xenia.Application.Modules;

/// <summary>
/// Manages the global Xenia module registry.
///
/// The registry is the source of truth for which modules are installed on the platform.
/// Per-tenant enablement is managed via <see cref="ITenantModuleRegistry"/>.
///
/// Implementations must prevent duplicate module registration.
/// </summary>
public interface IModuleRegistry
{
    /// <summary>
    /// Registers a new module. Throws if a module with the same key already exists.
    /// </summary>
    Task RegisterModuleAsync(
        string moduleKey,
        string name,
        string version,
        string description,
        string configurationNamespace,
        CancellationToken ct = default);

    /// <summary>Returns all registered modules.</summary>
    Task<IReadOnlyList<ModuleDto>> GetModulesAsync(CancellationToken ct = default);

    /// <summary>Returns a single module by key, or null if not found.</summary>
    Task<ModuleDto?> GetModuleAsync(string moduleKey, CancellationToken ct = default);

    /// <summary>Enables a module globally. No-op if already enabled.</summary>
    Task EnableModuleAsync(string moduleKey, CancellationToken ct = default);

    /// <summary>Disables a module globally. No-op if already disabled.</summary>
    Task DisableModuleAsync(string moduleKey, CancellationToken ct = default);
}

/// <summary>
/// Manages per-tenant module enablement within the Xenia platform.
/// All methods require an explicit tenant ID — no global tenant queries are permitted.
/// </summary>
public interface ITenantModuleRegistry
{
    /// <summary>
    /// Returns all module states for the specified tenant.
    /// Only returns entries where the global module exists.
    /// </summary>
    Task<IReadOnlyList<TenantModuleDto>> GetTenantModulesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Enables a module for a specific tenant.
    /// Creates the tenant-module record if it does not exist.
    /// </summary>
    Task EnableModuleForTenantAsync(Guid tenantId, string moduleKey, CancellationToken ct = default);

    /// <summary>
    /// Disables a module for a specific tenant.
    /// Creates the tenant-module record if it does not exist.
    /// </summary>
    Task DisableModuleForTenantAsync(Guid tenantId, string moduleKey, CancellationToken ct = default);
}
