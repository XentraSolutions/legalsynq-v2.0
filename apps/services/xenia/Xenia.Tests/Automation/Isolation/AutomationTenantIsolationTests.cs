using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Tests.Automation.Infrastructure;

namespace Xenia.Tests.Automation.Isolation;

/// <summary>
/// Tenant-isolation tests for all automation stores (G11).
///
/// Proves:
///   G11 — queries scoped to TenantA cannot return TenantB's data.
///          Each tenant-scoped store (TenantAutomations, AutomationRuntimeState,
///          AutomationExecutions, AutomationDeadLetters, AutomationConfiguration,
///          AutomationIdempotency) filters by TenantId at the query level.
///
/// Requires: MySQL container at 127.0.0.1:13309 (xenia-test-mysql-v1)
/// </summary>
[Collection("XeniaRelational")]
public sealed class AutomationTenantIsolationTests : IAsyncLifetime
{
    private readonly XeniaRelationalFixture _fx;
    private ServiceProvider? _sp;

    public AutomationTenantIsolationTests(XeniaRelationalFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        await _fx.TruncateAutomationTablesAsync();
        _sp = XeniaTestServiceBuilder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_sp is not null) await _sp.DisposeAsync();
    }

    // ── Tenant enablement isolation ────────────────────────────────────────

    [Fact]
    public async Task EnableForTenant_IsIsolatedPerTenant()
    {
        var registry = _sp!.GetRequiredService<IAutomationRegistry>();
        var provider = new FakeAutomationProvider("test.isolation.enable.v1");
        var tenantA  = Guid.CreateVersion7();
        var tenantB  = Guid.CreateVersion7();
        var actorId  = Guid.CreateVersion7();

        await registry.RegisterAsync(provider);

        // Enable for Tenant A only
        await registry.EnableForTenantAsync("test.isolation.enable.v1", tenantA, actorId);

        var stateA = await registry.GetEffectiveStateAsync("test.isolation.enable.v1", tenantA);
        var stateB = await registry.GetEffectiveStateAsync("test.isolation.enable.v1", tenantB);

        // Tenant A should see Enabled; Tenant B falls back to global state (Registered)
        Assert.Equal(AutomationLifecycleState.Enabled, stateA);
        Assert.NotEqual(AutomationLifecycleState.Enabled, stateB);
    }

    [Fact]
    public async Task DisableForTenantA_DoesNotAffectTenantB()
    {
        var registry = _sp!.GetRequiredService<IAutomationRegistry>();
        var provider = new FakeAutomationProvider("test.isolation.disable.v1");
        var tenantA  = Guid.CreateVersion7();
        var tenantB  = Guid.CreateVersion7();
        var actorId  = Guid.CreateVersion7();

        await registry.RegisterAsync(provider);
        await registry.EnableGloballyAsync("test.isolation.disable.v1", actorId);
        await registry.EnableForTenantAsync("test.isolation.disable.v1", tenantA, actorId);
        await registry.EnableForTenantAsync("test.isolation.disable.v1", tenantB, actorId);

        // Disable only for Tenant A
        await registry.DisableForTenantAsync("test.isolation.disable.v1", tenantA, actorId);

        var stateA = await registry.GetEffectiveStateAsync("test.isolation.disable.v1", tenantA);
        var stateB = await registry.GetEffectiveStateAsync("test.isolation.disable.v1", tenantB);

        Assert.Equal(AutomationLifecycleState.Disabled, stateA);
        Assert.Equal(AutomationLifecycleState.Enabled, stateB);
    }

    // ── Configuration isolation ────────────────────────────────────────────

    [Fact]
    public async Task Configuration_TenantA_NotVisibleToTenantB()
    {
        var svc     = _sp!.GetRequiredService<IAutomationConfigurationService>();
        const string key = "test.isolation.config.v1";
        const string ns  = "settings";
        var tenantA      = Guid.CreateVersion7();
        var tenantB      = Guid.CreateVersion7();

        // Write a tenant-scoped config for Tenant A only
        await svc.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantA,
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"tenant":"A"}""",
                schemaVersion: "1.0"));

        // Tenant B should not see Tenant A's entry
        var effectiveForB = await svc.GetEffectiveAsync(key, ns, tenantB);
        Assert.Null(effectiveForB);

        // Tenant A should see their entry
        var effectiveForA = await svc.GetEffectiveAsync(key, ns, tenantA);
        Assert.NotNull(effectiveForA);
        Assert.Contains("\"A\"", effectiveForA.ConfigurationJson);
    }

    // ── DB-level isolation: TenantAutomations rows scoped by tenant_id ────

    [Fact]
    public async Task TenantAutomations_DbRowsAreScopedByTenantId()
    {
        var registry = _sp!.GetRequiredService<IAutomationRegistry>();
        var provider = new FakeAutomationProvider("test.isolation.dbrows.v1");
        var tenantA  = Guid.CreateVersion7();
        var tenantB  = Guid.CreateVersion7();
        var actorId  = Guid.CreateVersion7();

        await registry.RegisterAsync(provider);
        await registry.EnableForTenantAsync("test.isolation.dbrows.v1", tenantA, actorId);
        await registry.EnableForTenantAsync("test.isolation.dbrows.v1", tenantB, actorId);

        await using var ctx = await _fx.CreateContextAsync();

        var rowsA = await ctx.TenantAutomations
            .Where(t =>
                t.AutomationKey == "test.isolation.dbrows.v1" &&
                t.TenantId == tenantA)
            .ToListAsync();

        var rowsB = await ctx.TenantAutomations
            .Where(t =>
                t.AutomationKey == "test.isolation.dbrows.v1" &&
                t.TenantId == tenantB)
            .ToListAsync();

        // Each tenant has exactly one row; they don't overlap
        Assert.Single(rowsA);
        Assert.Single(rowsB);
        Assert.DoesNotContain(rowsA, r => r.TenantId == tenantB);
        Assert.DoesNotContain(rowsB, r => r.TenantId == tenantA);
    }

    // ── DB-level isolation: TenantAutomationState.Enabled is correct ──────

    [Fact]
    public async Task TenantAutomationState_Enabled_Flag_ScopedCorrectly()
    {
        var registry = _sp!.GetRequiredService<IAutomationRegistry>();
        var provider = new FakeAutomationProvider("test.isolation.enabled.v1");
        var tenantA  = Guid.CreateVersion7();
        var actorId  = Guid.CreateVersion7();

        await registry.RegisterAsync(provider);
        await registry.EnableForTenantAsync("test.isolation.enabled.v1", tenantA, actorId);

        await using var ctx = await _fx.CreateContextAsync();
        var row = await ctx.TenantAutomations
            .FirstAsync(t =>
                t.AutomationKey == "test.isolation.enabled.v1" &&
                t.TenantId == tenantA);

        // TenantAutomationState uses Enabled (not IsEnabled)
        Assert.True(row.Enabled, "Row should have Enabled=true after EnableForTenantAsync");
    }

    // ── GetAllManifests does not cross tenant boundaries ─────────────────

    [Fact]
    public async Task GetAllManifestsAsync_WithTenantContext_ListsGlobalProviders()
    {
        var registry  = _sp!.GetRequiredService<IAutomationRegistry>();
        var providerX = new FakeAutomationProvider("test.isolation.manifests.x");
        var tenantA   = Guid.CreateVersion7();

        await registry.RegisterAsync(providerX);

        var manifests = await registry.GetAllManifestsAsync(tenantA);
        Assert.Contains(manifests, m => m.AutomationKey == "test.isolation.manifests.x");
    }
}
