using Xenia.Application.Modules;
using Xunit;

namespace Xenia.Tests.Modules;

/// <summary>
/// Tests for the EffectiveModuleDto effective-state computation.
///
/// Rule: EffectiveEnabled = GlobalEnabled AND TenantEnabled.
/// A globally-disabled module cannot be activated by a tenant.
/// A globally-enabled module that the tenant disabled is also not effective.
/// Only when BOTH are true is the module effectively enabled.
/// </summary>
public sealed class EffectiveModuleStateTests
{
    private static ModuleDto MakeGlobal(string key, bool globalEnabled) => new()
    {
        Id = Guid.CreateVersion7(),
        ModuleKey = key,
        Name = key,
        Version = "1.0.0",
        Description = string.Empty,
        GlobalEnabled = globalEnabled,
        Status = "Unknown",
        ConfigurationNamespace = key,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    private static TenantModuleDto MakeTenant(string key, bool tenantEnabled) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = Guid.CreateVersion7(),
        ModuleKey = key,
        Enabled = tenantEnabled,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    // ── Core logic ────────────────────────────────────────────────────────────

    [Fact]
    public void GlobalEnabled_TenantEnabled_EffectiveEnabled()
    {
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.sms", globalEnabled: true),
            MakeTenant("xenia.sms", tenantEnabled: true));

        Assert.True(effective.GlobalEnabled);
        Assert.True(effective.TenantEnabled);
        Assert.True(effective.EffectiveEnabled);
    }

    [Fact]
    public void GlobalDisabled_TenantEnabled_NotEffective()
    {
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.sms", globalEnabled: false),
            MakeTenant("xenia.sms", tenantEnabled: true));

        Assert.False(effective.GlobalEnabled);
        Assert.True(effective.TenantEnabled);
        Assert.False(effective.EffectiveEnabled);
    }

    [Fact]
    public void GlobalEnabled_TenantDisabled_NotEffective()
    {
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.sms", globalEnabled: true),
            MakeTenant("xenia.sms", tenantEnabled: false));

        Assert.True(effective.GlobalEnabled);
        Assert.False(effective.TenantEnabled);
        Assert.False(effective.EffectiveEnabled);
    }

    [Fact]
    public void GlobalDisabled_TenantDisabled_NotEffective()
    {
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.sms", globalEnabled: false),
            MakeTenant("xenia.sms", tenantEnabled: false));

        Assert.False(effective.GlobalEnabled);
        Assert.False(effective.TenantEnabled);
        Assert.False(effective.EffectiveEnabled);
    }

    [Fact]
    public void NoTenantOverride_DefaultsToTenantDisabled()
    {
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.sms", globalEnabled: true),
            tenant: null); // tenant has no record

        Assert.True(effective.GlobalEnabled);
        Assert.False(effective.TenantEnabled); // defaults to false
        Assert.False(effective.EffectiveEnabled); // no tenant override = not effective
    }

    // ── Property projection ───────────────────────────────────────────────────

    [Fact]
    public void From_ProjectsModuleKey()
    {
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.test", globalEnabled: true), null);

        Assert.Equal("xenia.test", effective.ModuleKey);
    }

    [Fact]
    public void From_ProjectsName()
    {
        var global = MakeGlobal("xenia.test", globalEnabled: true) with { Name = "Test Module" };
        var effective = EffectiveModuleDto.From(global, null);

        Assert.Equal("Test Module", effective.Name);
    }

    [Fact]
    public void From_ProjectsVersion()
    {
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.test", globalEnabled: true), null);

        Assert.Equal("1.0.0", effective.Version);
    }

    // ── Isolation between tenants ─────────────────────────────────────────────

    [Fact]
    public void TenantA_EnabledModule_DoesNotAffectTenantB()
    {
        var global = MakeGlobal("xenia.sms", globalEnabled: true);

        var tenantAEntry = MakeTenant("xenia.sms", tenantEnabled: true);
        var tenantBEntry = MakeTenant("xenia.sms", tenantEnabled: false);

        var effectiveA = EffectiveModuleDto.From(global, tenantAEntry);
        var effectiveB = EffectiveModuleDto.From(global, tenantBEntry);

        Assert.True(effectiveA.EffectiveEnabled);
        Assert.False(effectiveB.EffectiveEnabled);
    }

    // ── Global module state tests ─────────────────────────────────────────────

    [Fact]
    public void GlobalModuleState_GlobalEnabled_IsTrue_WhenSet()
    {
        var global = MakeGlobal("xenia.email", globalEnabled: true);
        var effective = EffectiveModuleDto.From(global, null);
        Assert.True(effective.GlobalEnabled);
    }

    [Fact]
    public void GlobalModuleState_GlobalEnabled_IsFalse_WhenNotSet()
    {
        var global = MakeGlobal("xenia.email", globalEnabled: false);
        var effective = EffectiveModuleDto.From(global, null);
        Assert.False(effective.GlobalEnabled);
    }

    // ── Unknown module ────────────────────────────────────────────────────────

    [Fact]
    public void UnknownModule_NotInGlobalRegistry_IsNotEffective()
    {
        // This models what happens when a tenant record references a moduleKey
        // that is not registered globally. The global state drives EffectiveEnabled.
        // In this scenario the global record would not be found → module is not effective.
        // We simulate by using a disabled global.
        var effective = EffectiveModuleDto.From(
            MakeGlobal("xenia.unknown", globalEnabled: false),
            MakeTenant("xenia.unknown", tenantEnabled: true));

        Assert.False(effective.EffectiveEnabled,
            "A module not globally enabled cannot be activated by a tenant override.");
    }
}
