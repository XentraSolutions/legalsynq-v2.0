using Xenia.Application.Automation;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

internal sealed class DefaultAutomationDiscoveryService : IAutomationDiscoveryService
{
    private readonly IAutomationRegistry _registry;

    public DefaultAutomationDiscoveryService(IAutomationRegistry registry) => _registry = registry;

    public Task<IReadOnlyList<AutomationManifest>> DiscoverAllAsync(Guid? tenantId, CancellationToken ct = default) =>
        _registry.GetAllManifestsAsync(tenantId, ct);

    public Task<AutomationManifest?> DiscoverByKeyAsync(string automationKey, CancellationToken ct = default) =>
        _registry.GetManifestAsync(automationKey, ct);

    public async Task<bool> IsAvailableAsync(string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        var state = await _registry.GetEffectiveStateAsync(automationKey, tenantId, ct);
        return state is AutomationLifecycleState.Enabled;
    }
}
