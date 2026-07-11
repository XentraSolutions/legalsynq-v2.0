using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed implementation of <see cref="IAutomationRegistry"/>.
///
/// Replaces <see cref="InMemoryAutomationRegistry"/> for production use.
///
/// Dual-layer design:
/// - Provider references are code-defined and held in a process-local
///   <see cref="ConcurrentDictionary{TKey,TValue}"/>; they survive the process
///   lifetime and are re-registered at every startup by
///   <see cref="AutomationRegistrationWorker"/>.
/// - All mutable lifecycle state persists to MySQL:
///   · xn_automation_registry      — platform-level registration row
///   · xn_automation_versions      — per-version manifest snapshot
///   · xn_automation_runtime_state — via <see cref="IAutomationRuntimeStateStore"/> (already EF)
///   · xn_tenant_automations       — per-tenant enable/disable overrides
///
/// Singleton — uses <see cref="IDbContextFactory{T}"/> for short-lived contexts
/// to avoid captive-dependency issues.
/// </summary>
internal sealed class EfAutomationRegistry : IAutomationRegistry
{
    private const string ManifestSchemaVersion = "1.0";

    private readonly ConcurrentDictionary<string, IAutomationProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<XeniaDbContext> _contextFactory;
    private readonly IAutomationRuntimeStateStore _stateStore;
    private readonly IAutomationEventPublisher _events;
    private readonly ILogger<EfAutomationRegistry> _logger;

    public EfAutomationRegistry(
        IDbContextFactory<XeniaDbContext> contextFactory,
        IAutomationRuntimeStateStore stateStore,
        IAutomationEventPublisher events,
        ILogger<EfAutomationRegistry> logger)
    {
        _contextFactory = contextFactory;
        _stateStore     = stateStore;
        _events         = events;
        _logger         = logger;
    }

    public async Task<RegistrationResult> RegisterAsync(
        IAutomationProvider provider, CancellationToken ct = default)
    {
        var key = provider.AutomationKey;

        if (_providers.TryGetValue(key, out var existing))
        {
            if (existing.Version == provider.Version)
            {
                await ReconcileRegistrationAsync(provider, ct);
                return RegistrationResult.Duplicate();
            }
            return RegistrationResult.Conflict(
                $"Automation '{key}' already registered with version '{existing.Version}'; " +
                $"cannot re-register with '{provider.Version}'.");
        }

        if (!_providers.TryAdd(key, provider))
        {
            if (_providers.TryGetValue(key, out var race) && race.Version == provider.Version)
                return RegistrationResult.Duplicate();
            return RegistrationResult.Conflict(
                $"Concurrent registration conflict for key '{key}'.");
        }

        await UpsertRegistrationAsync(provider, ct);

        var state = await _stateStore.GetAsync(key, null, ct);
        if (state is null)
        {
            state = AutomationRuntimeState.Create(
                key, provider.Version, null, AutomationLifecycleState.Registered);
            await _stateStore.UpsertAsync(state, ct);
        }

        await _events.PublishRegisteredAsync(key, provider.Version, ct);
        _logger.LogInformation(
            "Automation registered durably: key={Key} version={Version}", key, provider.Version);
        return RegistrationResult.Success();
    }

    public async Task<IReadOnlyList<AutomationManifest>> GetAllManifestsAsync(
        Guid? tenantId, CancellationToken ct = default)
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

    public Task<AutomationManifest?> GetManifestAsync(
        string automationKey, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(automationKey, out var p))
            return Task.FromResult<AutomationManifest?>(null);
        return Task.FromResult<AutomationManifest?>(p.GetManifest());
    }

    public Task<AutomationRuntimeState?> GetRuntimeStateAsync(
        string automationKey, Guid? tenantId, CancellationToken ct = default) =>
        _stateStore.GetAsync(automationKey, tenantId, ct);

    public async Task<bool> EnableGloballyAsync(
        string automationKey, Guid actorId, CancellationToken ct = default)
    {
        if (!await UpdateRegistrationLifecycleAsync(automationKey, enable: true, ct)) return false;

        var state = await GetOrCreateStateAsync(automationKey, ct);
        if (state is null) return false;
        state.SetGlobalState(AutomationLifecycleState.Enabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishEnabledAsync(automationKey, state.AutomationVersion, null, actorId, ct);
        return true;
    }

    public async Task<bool> DisableGloballyAsync(
        string automationKey, Guid actorId, CancellationToken ct = default)
    {
        if (!await UpdateRegistrationLifecycleAsync(automationKey, enable: false, ct)) return false;

        var state = await GetOrCreateStateAsync(automationKey, ct);
        if (state is null) return false;
        state.SetGlobalState(AutomationLifecycleState.Disabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishDisabledAsync(automationKey, state.AutomationVersion, null, actorId, ct);
        return true;
    }

    public async Task<bool> EnableForTenantAsync(
        string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        await UpsertTenantStateAsync(automationKey, tenantId, enable: true, ct);

        var state = await GetOrCreateStateAsync(automationKey, ct, tenantId);
        if (state is null) return false;
        state.SetTenantState(AutomationLifecycleState.Enabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishEnabledAsync(automationKey, state.AutomationVersion, tenantId, actorId, ct);
        return true;
    }

    public async Task<bool> DisableForTenantAsync(
        string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        await UpsertTenantStateAsync(automationKey, tenantId, enable: false, ct);

        var state = await GetOrCreateStateAsync(automationKey, ct, tenantId);
        if (state is null) return false;
        state.SetTenantState(AutomationLifecycleState.Disabled);
        await _stateStore.UpsertAsync(state, ct);
        await _events.PublishDisabledAsync(automationKey, state.AutomationVersion, tenantId, actorId, ct);
        return true;
    }

    public async Task<AutomationLifecycleState> GetEffectiveStateAsync(
        string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        var state = await _stateStore.GetAsync(automationKey, tenantId, ct);
        return state?.EffectiveState ?? AutomationLifecycleState.Unavailable;
    }

    public Task<IReadOnlyList<AutomationDependency>> GetDependenciesAsync(
        string automationKey, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(automationKey, out var p))
            return Task.FromResult<IReadOnlyList<AutomationDependency>>([]);
        return Task.FromResult(p.GetDependencies());
    }

    public IAutomationProvider? GetProvider(string automationKey) =>
        _providers.GetValueOrDefault(automationKey);

    public IReadOnlyList<IAutomationProvider> GetAllProviders() =>
        [.. _providers.Values];

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task UpsertRegistrationAsync(IAutomationProvider provider, CancellationToken ct)
    {
        var manifest     = provider.GetManifest();
        var manifestJson = JsonSerializer.Serialize(manifest);
        var manifestHash = ComputeManifestHash(manifestJson);

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var reg = await ctx.AutomationRegistry
            .FirstOrDefaultAsync(r => r.AutomationKey == provider.AutomationKey, ct);

        if (reg is null)
        {
            reg = AutomationRegistration.Create(
                provider.AutomationKey,
                manifest.Provider,
                manifest.Category,
                provider.Version,
                manifestHash,
                manifest.MinimumPlatformVersion);
            ctx.AutomationRegistry.Add(reg);
        }
        else
        {
            reg.Reconcile(provider.Version, manifestHash, DateTime.UtcNow);
            ctx.AutomationRegistry.Update(reg);
        }

        var ver = await ctx.AutomationVersions
            .FirstOrDefaultAsync(v =>
                v.AutomationKey == provider.AutomationKey &&
                v.Version == provider.Version, ct);

        if (ver is null)
        {
            ver = AutomationVersionRecord.Create(
                provider.AutomationKey,
                provider.Version,
                manifestJson,
                ManifestSchemaVersion);
            ver.Activate();
            ctx.AutomationVersions.Add(ver);
        }
        else if (ver.Status != AutomationVersionStatus.Active)
        {
            ver.Activate();
            ctx.AutomationVersions.Update(ver);
        }

        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Failed to upsert durable registration for key={Key}; " +
                "provider dict already updated — continuing", provider.AutomationKey);
        }
    }

    private async Task ReconcileRegistrationAsync(IAutomationProvider provider, CancellationToken ct)
    {
        var manifest     = provider.GetManifest();
        var manifestJson = JsonSerializer.Serialize(manifest);
        var manifestHash = ComputeManifestHash(manifestJson);

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var reg = await ctx.AutomationRegistry
            .FirstOrDefaultAsync(r => r.AutomationKey == provider.AutomationKey, ct);

        if (reg is null)
        {
            reg = AutomationRegistration.Create(
                provider.AutomationKey,
                manifest.Provider,
                manifest.Category,
                provider.Version,
                manifestHash,
                manifest.MinimumPlatformVersion);
            ctx.AutomationRegistry.Add(reg);
        }
        else if (reg.ManifestHash != manifestHash)
        {
            reg.Reconcile(provider.Version, manifestHash, DateTime.UtcNow);
            ctx.AutomationRegistry.Update(reg);
        }
        else
        {
            return;
        }

        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Reconcile upsert failed for key={Key} — non-fatal", provider.AutomationKey);
        }
    }

    private async Task<bool> UpdateRegistrationLifecycleAsync(
        string automationKey, bool enable, CancellationToken ct)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var reg = await ctx.AutomationRegistry
            .FirstOrDefaultAsync(r => r.AutomationKey == automationKey, ct);

        if (reg is null) return false;

        if (enable)
            reg.Enable();
        else
            reg.Disable();

        try
        {
            await ctx.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict updating lifecycle for key={Key}", automationKey);
            return false;
        }
    }

    private async Task UpsertTenantStateAsync(
        string automationKey, Guid tenantId, bool enable, CancellationToken ct)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var tenantState = await ctx.TenantAutomations
            .FirstOrDefaultAsync(t =>
                t.TenantId == tenantId &&
                t.AutomationKey == automationKey, ct);

        if (tenantState is null)
        {
            tenantState = TenantAutomationState.Create(tenantId, automationKey, enable);
            ctx.TenantAutomations.Add(tenantState);
        }
        else
        {
            if (enable)
                tenantState.Enable();
            else
                tenantState.Disable();
            ctx.TenantAutomations.Update(tenantState);
        }

        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict upserting tenant state for key={Key} tenant={TenantId}",
                automationKey, tenantId);
        }
    }

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

    private static string ComputeManifestHash(string manifestJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..64];
    }
}
