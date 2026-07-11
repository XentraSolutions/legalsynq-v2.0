using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Manages durable configuration entries for automation providers.
///
/// Configuration is stored at either Platform scope (applies to all tenants)
/// or Tenant scope (tenant-specific override). No resolved secret values
/// are stored — only reference keys via SecretReferencesJson.
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
}
