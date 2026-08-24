using Microsoft.Extensions.DependencyInjection;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Tests.Automation.Infrastructure;

namespace Xenia.Tests.Automation.Stores;

/// <summary>
/// Relational tests for EfAutomationConfigurationService (G7).
///
/// Proves:
///   G7 — configuration precedence is enforced:
///         Tenant scope overrides Platform scope;
///         Platform scope applies when no Tenant entry exists;
///         null is returned when no configuration exists at any scope.
///
/// Requires: MySQL container at 127.0.0.1:13309 (xenia-test-mysql-v1)
/// </summary>
[Collection("XeniaRelational")]
public sealed class AutomationConfigurationPrecedenceTests : IAsyncLifetime
{
    private readonly XeniaRelationalFixture _fx;
    private ServiceProvider? _sp;
    private IAutomationConfigurationService _svc = null!;

    public AutomationConfigurationPrecedenceTests(XeniaRelationalFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        await _fx.TruncateAutomationTablesAsync();
        _sp  = XeniaTestServiceBuilder.Build();
        _svc = _sp.GetRequiredService<IAutomationConfigurationService>();
    }

    public async Task DisposeAsync()
    {
        if (_sp is not null) await _sp.DisposeAsync();
    }

    // ── G7-A: Tenant scope overrides Platform scope ────────────────────────

    [Fact]
    public async Task GetEffectiveAsync_TenantScopeOverridesPlatformScope()
    {
        const string key      = "test.config.prec.tenantwin.v1";
        const string ns       = "settings";
        var          tenantId = Guid.CreateVersion7();

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreatePlatform(
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"source":"platform"}""",
                schemaVersion: "1.0"));

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantId,
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"source":"tenant"}""",
                schemaVersion: "1.0"));

        var effective = await _svc.GetEffectiveAsync(key, ns, tenantId);

        Assert.NotNull(effective);
        Assert.Equal(AutomationConfigurationScope.Tenant, effective.ScopeType);
        Assert.Equal(tenantId, effective.TenantId);
        Assert.Contains("tenant", effective.ConfigurationJson);
    }

    // ── G7-B: Platform scope applies when no Tenant override exists ────────

    [Fact]
    public async Task GetEffectiveAsync_FallsBackToPlatformWhenNoTenantEntry()
    {
        const string key      = "test.config.prec.pfallback.v1";
        const string ns       = "settings";
        var          tenantId = Guid.CreateVersion7();

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreatePlatform(
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"source":"platform"}""",
                schemaVersion: "1.0"));

        // No tenant-specific entry; should fall back to platform
        var effective = await _svc.GetEffectiveAsync(key, ns, tenantId);

        Assert.NotNull(effective);
        Assert.Equal(AutomationConfigurationScope.Platform, effective.ScopeType);
        Assert.Null(effective.TenantId);
        Assert.Contains("platform", effective.ConfigurationJson);
    }

    // ── G7-C: Returns null when no configuration exists at any scope ───────

    [Fact]
    public async Task GetEffectiveAsync_ReturnsNullWhenNothingExists()
    {
        const string key      = "test.config.prec.missing.v1";
        const string ns       = "settings";
        var          tenantId = Guid.CreateVersion7();

        var effective = await _svc.GetEffectiveAsync(key, ns, tenantId);

        Assert.Null(effective);
    }

    // ── G7-D: Null tenantId (global context) returns Platform scope ────────

    [Fact]
    public async Task GetEffectiveAsync_NullTenantId_ReturnsPlatformScope()
    {
        const string key = "test.config.prec.globalctx.v1";
        const string ns  = "settings";

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreatePlatform(
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"env":"global","scope":"platform"}""",
                schemaVersion: "1.0"));

        var effective = await _svc.GetEffectiveAsync(key, ns, tenantId: null);

        Assert.NotNull(effective);
        Assert.Equal(AutomationConfigurationScope.Platform, effective.ScopeType);
        Assert.Null(effective.TenantId);
    }

    // ── G7-E: Upsert is idempotent ─────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_SecondWriteUpdatesExistingRow()
    {
        const string key      = "test.config.prec.upsert.v1";
        const string ns       = "settings";
        var          tenantId = Guid.CreateVersion7();

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantId,
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"v":1}""",
                schemaVersion: "1.0"));

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantId,
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"v":2}""",
                schemaVersion: "1.0"));

        // Only one Tenant-scope entry should exist
        var list = await _svc.ListAsync(key, tenantId);
        var tenantEntries = list
            .Where(e => e.ScopeType == AutomationConfigurationScope.Tenant)
            .ToList();
        Assert.Single(tenantEntries);

        // Content should reflect the second write
        var effective = await _svc.GetEffectiveAsync(key, ns, tenantId);
        Assert.NotNull(effective);
        Assert.Contains("\"v\":2", effective.ConfigurationJson);
    }

    // ── G7-F: Delete removes entry ─────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesEntry_SubsequentGetReturnsNull()
    {
        const string key      = "test.config.prec.delete.v1";
        const string ns       = "settings";
        var          tenantId = Guid.CreateVersion7();

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantId,
                automationKey: key,
                configurationNamespace: ns,
                configurationJson: """{"x":1}""",
                schemaVersion: "1.0"));

        var deleted = await _svc.DeleteAsync(
            key, ns, AutomationConfigurationScope.Tenant, tenantId);
        Assert.True(deleted);

        var effective = await _svc.GetEffectiveAsync(key, ns, tenantId);
        Assert.Null(effective);
    }

    // ── G7-G: Different namespaces are isolated from each other ───────────

    [Fact]
    public async Task GetEffectiveAsync_DifferentNamespaces_DoNotInterfere()
    {
        const string key      = "test.config.prec.ns.v1";
        const string ns1      = "alpha";
        const string ns2      = "beta";
        var          tenantId = Guid.CreateVersion7();

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantId,
                automationKey: key,
                configurationNamespace: ns1,
                configurationJson: """{"ns":"alpha"}""",
                schemaVersion: "1.0"));

        await _svc.UpsertAsync(
            AutomationConfigurationEntry.CreateTenant(
                tenantId: tenantId,
                automationKey: key,
                configurationNamespace: ns2,
                configurationJson: """{"ns":"beta"}""",
                schemaVersion: "1.0"));

        var alpha = await _svc.GetEffectiveAsync(key, ns1, tenantId);
        var beta  = await _svc.GetEffectiveAsync(key, ns2, tenantId);

        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        Assert.Contains("alpha", alpha.ConfigurationJson);
        Assert.Contains("beta", beta.ConfigurationJson);
    }
}
