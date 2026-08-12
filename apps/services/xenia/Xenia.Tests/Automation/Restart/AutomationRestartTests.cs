using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;
using Xenia.Tests.Automation.Infrastructure;

namespace Xenia.Tests.Automation.Restart;

/// <summary>
/// Restart-survival tests for the automation runtime (G13).
///
/// Proves:
///   G13 — state written by Instance A remains intact and queryable when
///          Instance B starts fresh (simulating a service restart).
///
/// Key guarantee: rows are in MySQL, not in-memory, so a restart does not
/// lose Queued executions, configuration entries, or registry registrations.
///
/// Requires: MySQL container at 127.0.0.1:13309 (xenia-test-mysql-v1)
/// </summary>
[Collection("XeniaRelational")]
public sealed class AutomationRestartTests : IAsyncLifetime
{
    private readonly XeniaRelationalFixture _fx;

    public AutomationRestartTests(XeniaRelationalFixture fx) => _fx = fx;

    public async Task InitializeAsync() => await _fx.TruncateAutomationTablesAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── G13-A: Registry rows survive across instances ──────────────────────

    [Fact]
    public async Task RegistryRow_SurvivesSimulatedRestart_NewInstanceSeesRow()
    {
        // Instance A: register a provider (writes to DB)
        await using var spA = XeniaTestServiceBuilder.Build(instanceId: "A");
        var registryA = spA.GetRequiredService<IAutomationRegistry>();
        var provider  = new FakeAutomationProvider("test.restart.registry.v1");
        await registryA.RegisterAsync(provider);

        // Simulate restart: dispose Instance A, create Instance B
        await spA.DisposeAsync();

        // Instance B: read directly from DB (bypass in-memory)
        await using var ctx = await _fx.CreateContextAsync();
        var row = await ctx.AutomationRegistry
            .FirstOrDefaultAsync(r => r.AutomationKey == "test.restart.registry.v1");

        Assert.NotNull(row);
        Assert.Equal("1.0.0", row.CurrentVersion);
        Assert.Equal("FakeProvider", row.Provider);
    }

    // ── G13-B: Configuration entries survive restart ───────────────────────

    [Fact]
    public async Task ConfigurationEntry_SurvivesSimulatedRestart()
    {
        const string key      = "test.restart.config.v1";
        const string ns       = "core";
        var          tenantId = Guid.CreateVersion7();

        // Instance A: write config
        await using var spA = XeniaTestServiceBuilder.Build(instanceId: "A");
        var cfgA = spA.GetRequiredService<IAutomationConfigurationService>();
        await cfgA.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantId,
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"key":"restart-value"}""",
                schemaVersion: "1.0"));
        await spA.DisposeAsync();

        // Instance B: read the same config
        await using var spB = XeniaTestServiceBuilder.Build(instanceId: "B");
        var cfgB = spB.GetRequiredService<IAutomationConfigurationService>();

        var effective = await cfgB.GetEffectiveAsync(key, ns, tenantId);

        Assert.NotNull(effective);
        Assert.Contains("restart-value", effective.ConfigurationJson);
    }

    // ── G13-C: Tenant enablement survives restart ──────────────────────────

    [Fact]
    public async Task TenantEnablement_SurvivesSimulatedRestart()
    {
        const string key      = "test.restart.enable.v1";
        var          tenantId = Guid.CreateVersion7();
        var          actorId  = Guid.CreateVersion7();

        // Instance A: register and enable for tenant
        await using var spA = XeniaTestServiceBuilder.Build(instanceId: "A");
        var regA = spA.GetRequiredService<IAutomationRegistry>();
        var pa   = new FakeAutomationProvider(key);
        await regA.RegisterAsync(pa);
        await regA.EnableForTenantAsync(key, tenantId, actorId);
        await spA.DisposeAsync();

        // Instance B: verify the tenant-scoped row is in DB
        await using var ctx = await _fx.CreateContextAsync();
        var row = await ctx.TenantAutomations
            .FirstOrDefaultAsync(t => t.AutomationKey == key && t.TenantId == tenantId);

        Assert.NotNull(row);
        Assert.True(row.IsEnabled);
    }

    // ── G13-D: Reconciler on restart marks and restores providers ─────────

    [Fact]
    public async Task Reconciler_AfterRestart_CanRestoreProvidersThatComeBack()
    {
        // Instance A: register Alpha and Beta
        await using var spA = XeniaTestServiceBuilder.Build(instanceId: "A");
        var rA  = spA.GetRequiredService<IAutomationRegistry>();
        var rcA = spA.GetRequiredService<IAutomationRegistryReconciler>();
        var pa  = new FakeAutomationProvider("test.restart.recon.alpha");
        var pb  = new FakeAutomationProvider("test.restart.recon.beta");
        await rA.RegisterAsync(pa);
        await rA.RegisterAsync(pb);
        await rcA.ReconcileAsync();
        await spA.DisposeAsync();

        // Instance B: restart with ONLY Alpha — Beta absent → should be Unavailable
        await using var spB = XeniaTestServiceBuilder.Build(instanceId: "B");
        var rB  = spB.GetRequiredService<IAutomationRegistry>();
        var rcB = spB.GetRequiredService<IAutomationRegistryReconciler>();
        await rB.RegisterAsync(pa);
        var summaryB = await rcB.ReconcileAsync();
        Assert.True(summaryB.MarkedUnavailable >= 1);
        await spB.DisposeAsync();

        // Instance C: restart with BOTH — Beta should be restored
        await using var spC = XeniaTestServiceBuilder.Build(instanceId: "C");
        var rC  = spC.GetRequiredService<IAutomationRegistry>();
        var rcC = spC.GetRequiredService<IAutomationRegistryReconciler>();
        await rC.RegisterAsync(pa);
        await rC.RegisterAsync(pb);
        var summaryC = await rcC.ReconcileAsync();

        Assert.True(summaryC.Restored >= 1,
            $"Expected at least 1 provider restored on Instance C, got {summaryC.Restored}");

        await using var ctx = await _fx.CreateContextAsync();
        var betaRow = await ctx.AutomationRegistry
            .FirstAsync(r => r.AutomationKey == "test.restart.recon.beta");

        Assert.NotEqual(AutomationLifecycleState.Unavailable, betaRow.LifecycleStatus);
    }

    // ── G13-E: Row version is durable across restarts ─────────────────────

    [Fact]
    public async Task RowVersion_DurableAcrossRestart_ReflectsPreRestartMutations()
    {
        const string key     = "test.restart.rowversion.v1";
        var          actorId = Guid.CreateVersion7();

        // Instance A: register + enable (two mutations → row_version should be > 0)
        await using var spA = XeniaTestServiceBuilder.Build(instanceId: "A");
        var rA = spA.GetRequiredService<IAutomationRegistry>();
        await rA.RegisterAsync(new FakeAutomationProvider(key));
        await rA.EnableGloballyAsync(key, actorId);
        await spA.DisposeAsync();

        // Read row_version directly from DB (simulates "after restart" DB state)
        await using var ctx = await _fx.CreateContextAsync();
        var row = await ctx.AutomationRegistry.FirstAsync(r => r.AutomationKey == key);

        // After at least one mutation (enable), row_version must be > 0
        Assert.True(row.RowVersion > 0,
            $"Expected RowVersion > 0 after mutation, got {row.RowVersion}");
    }
}
