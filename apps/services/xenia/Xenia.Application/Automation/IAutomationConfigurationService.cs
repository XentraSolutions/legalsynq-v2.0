using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Manages durable configuration entries for automation providers.
///
/// Configuration is stored at either Platform scope (applies to all tenants)
/// or Tenant scope (tenant-specific override). No resolved secret values
/// are stored — only reference keys via SecretReferencesJson.
///
/// Precedence order (highest to lowest):
///   1. Tenant scope (tenantId-specific, namespace-specific)
///   2. Platform scope (no tenantId, namespace-specific)
///   3. null — no configuration found at any scope
/// </summary>
public interface IAutomationConfigurationService
{
    Task<AutomationConfigurationEntry?> GetAsync(
        string automationKey,
        string configurationNamespace,
        AutomationConfigurationScope scope,
        Guid? tenantId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AutomationConfigurationEntry>> ListAsync(
        string automationKey,
        Guid? tenantId,
        CancellationToken ct = default);

    Task<AutomationConfigurationEntry> UpsertAsync(
        AutomationConfigurationEntry entry,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        string automationKey,
        string configurationNamespace,
        AutomationConfigurationScope scope,
        Guid? tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the effective configuration entry for the given key and namespace,
    /// applying precedence: Tenant scope overrides Platform scope.
    ///
    /// Returns null when no configuration exists at any scope.
    ///
    /// This method satisfies G7: configuration precedence is enforced here so
    /// callers do not need to implement their own merge logic.
    /// </summary>
    Task<AutomationConfigurationEntry?> GetEffectiveAsync(
        string automationKey,
        string configurationNamespace,
        Guid? tenantId,
        CancellationToken ct = default);
}
