using System.Collections.Concurrent;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Phase 1 in-memory implementation.
/// EF Core persistence is added in Phase H (Migration 8) but not required for Phase 1 contracts.
/// This store is safe for single-node deployments; multi-node requires EF-backed store.
/// </summary>
internal sealed class InMemoryAutomationRuntimeStateStore : IAutomationRuntimeStateStore
{
    private readonly ConcurrentDictionary<string, AutomationRuntimeState> _states = new(StringComparer.OrdinalIgnoreCase);

    private static string MakeKey(string automationKey, Guid? tenantId) =>
        tenantId.HasValue ? $"{automationKey}::{tenantId}" : automationKey;

    public Task<AutomationRuntimeState?> GetAsync(string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        _states.TryGetValue(MakeKey(automationKey, tenantId), out var state);
        return Task.FromResult(state);
    }

    public Task UpsertAsync(AutomationRuntimeState state, CancellationToken ct = default)
    {
        _states[MakeKey(state.AutomationKey, state.TenantId)] = state;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AutomationRuntimeState>> ListAsync(Guid? tenantId, CancellationToken ct = default)
    {
        IReadOnlyList<AutomationRuntimeState> result = tenantId.HasValue
            ? [.. _states.Values.Where(s => s.TenantId == tenantId)]
            : [.. _states.Values];
        return Task.FromResult(result);
    }
}
