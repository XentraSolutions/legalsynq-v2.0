using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Discovers automation providers registered in the DI container.
/// Phase 1 uses DI-based static discovery; plugin-based discovery is out of scope.
/// </summary>
public interface IAutomationDiscoveryService
{
    Task<IReadOnlyList<AutomationManifest>> DiscoverAllAsync(Guid? tenantId, CancellationToken ct = default);
    Task<AutomationManifest?> DiscoverByKeyAsync(string automationKey, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(string automationKey, Guid? tenantId, CancellationToken ct = default);
}
