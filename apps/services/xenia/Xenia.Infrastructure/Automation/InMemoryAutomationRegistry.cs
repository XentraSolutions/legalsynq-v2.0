using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Thread-safe in-memory automation registry backed by DI-registered providers.
/// Runtime state (enable/disable overrides) is persisted via IAutomationRuntimeStateStore.
/// Phase 1: in-memory manifest cache, EF-persisted runtime state.
/// </summary>
internal sealed class InMemoryAutomationRegistry : IAutomationRegistry
{
    private readonly ConcurrentDictionary<string, IAutomationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAutomationRuntimeStateStore _stateStore;
    private readonly IAutomationEventPublisher _events;
    private readonly ILogger<InMemoryAutomationRegistry> _logger;

    public InMemoryAutomationRegistry(
        IAutomationRuntimeStateStore stateStore,
        IAutomationEventPublisher events,
        ILogger<InMemoryAutomationRegistry> logger)
    {
        _stateStore = stateStore;
        _events     = events;
        _logger     = logger;
    }

    public async Task<RegistrationResult> RegisterAsync(IAutomationProvider provider, CancellationToken ct = default)
    {
        var key = provider.AutomationKey;
        if (_providers.TryGetValue(key, out var existing))
        {
            if (existing.Version == provider.Version)
                return RegistrationResult.Duplicate();
            return RegistrationResult.Conflict(
                $"Automation '{key}' already registered with version '{existing.Version}'; cannot re-register with '{provider.Version}'.");
        }

        if (!_providers.TryAdd(key, provider))
        {
            if (_providers.TryGetValue(key, out var race) && race.Version == provider.Version)
                return RegistrationResult.Duplicate();
            return RegistrationResult.Conflict($"Concurrent registration conflict for key '{key}'.");
        }

        var state = await _stateStore.GetAsync(key, null, ct);
        if (state is null)
        {
            state = AutomationRuntimeState.Create(key, provider.Version, null, AutomationLifecycleState.Registered);
            await _stateStore.UpsertAsync(state, ct);
        }

        await _events.PublishRegisteredAsync(key, provider.Version, ct);
        _logger.LogInformation("Automation registered: key={Key} version={Version}", key, provider.Version);
        return RegistrationResult.Success();
    }

    public async Task<IReadOnlyList<AutomationManifest>> GetAllManifestsAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var result = new List<AutomationManifest>();
        foreach (var p in _providers.Values)
        {
            var manifest = p.GetManifest();
            var state = await _stateStore.GetAsync(p.AutomationKey, tenantId, ct);
            var effectiveState = state?.EffectiveState ?? AutomationLifecycleState.Registered;
            result.Add(manifest with { Status = effectiveState });
        }
        return result;
    }

    public Task<AutomationManifest?> GetManifestAsync(string automationKey, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(automationKey, out var p))
            return Task.FromResult<AutomationManifest?>(null);
        return Task.FromResult<AutomationManifest?>(p.GetManifest());
    }

    public Task<AutomationRuntimeState?> GetRuntimeStateAsync(string automationKey, Guid? tenantId, CancellationToken ct = default) =>
        _stateStore.GetAsync(automationKey, tenantId, ct);

    public async Task<bool> EnableGloballyAsync(string automationKey, Guid actorId, CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(automationKey, ct);
        if (state is null) return false;
        state.SetGlobalState(AutomationLifecycleState.Enabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishEnabledAsync(automationKey, state.AutomationVersion, null, actorId, ct);
        return true;
    }

    public async Task<bool> DisableGloballyAsync(string automationKey, Guid actorId, CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(automationKey, ct);
        if (state is null) return false;
        state.SetGlobalState(AutomationLifecycleState.Disabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishDisabledAsync(automationKey, state.AutomationVersion, null, actorId, ct);
        return true;
    }

    public async Task<bool> EnableForTenantAsync(string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(automationKey, ct, tenantId);
        if (state is null) return false;
        state.SetTenantState(AutomationLifecycleState.Enabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishEnabledAsync(automationKey, state.AutomationVersion, tenantId, actorId, ct);
        return true;
    }

    public async Task<bool> DisableForTenantAsync(string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(automationKey, ct, tenantId);
        if (state is null) return false;
        state.SetTenantState(AutomationLifecycleState.Disabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishDisabledAsync(automationKey, state.AutomationVersion, tenantId, actorId, ct);
        return true;
    }

    public async Task<AutomationLifecycleState> GetEffectiveStateAsync(string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        var state = await _stateStore.GetAsync(automationKey, tenantId, ct);
        return state?.EffectiveState ?? AutomationLifecycleState.Unavailable;
    }

    public Task<IReadOnlyList<AutomationDependency>> GetDependenciesAsync(string automationKey, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(automationKey, out var p))
            return Task.FromResult<IReadOnlyList<AutomationDependency>>([]);
        return Task.FromResult(p.GetDependencies());
    }

    public IAutomationProvider? GetProvider(string automationKey) =>
        _providers.GetValueOrDefault(automationKey);

    public IReadOnlyList<IAutomationProvider> GetAllProviders() =>
        [.. _providers.Values];

    private async Task<AutomationRuntimeState?> GetOrCreateStateAsync(
        string automationKey, CancellationToken ct, Guid? tenantId = null)
    {
        if (!_providers.TryGetValue(automationKey, out var p)) return null;
        var state = await _stateStore.GetAsync(automationKey, tenantId, ct);
        if (state is not null) return state;
        state = AutomationRuntimeState.Create(automationKey, p.Version, tenantId);
        await _stateStore.UpsertAsync(state, ct);
        return state;
    }
}
