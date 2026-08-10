using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.Email;
using Xenia.Infrastructure.Modules;
using Xenia.Infrastructure.Persistence;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for Email module registration in the Xenia module registry.
/// Uses InMemory database — no MySQL required.
/// </summary>
public sealed class EmailModuleRegistrationTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfModuleRegistry _registry;

    public EmailModuleRegistrationTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new XeniaDbContext(options);
        _registry = new EfModuleRegistry(_db, NullLogger<EfModuleRegistry>.Instance);
    }

    [Fact]
    public async Task EmailModule_RegistersWithCorrectKey()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey,
            EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion,
            EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);

        var module = await _registry.GetModuleAsync(EmailModuleKeys.ModuleKey);

        Assert.NotNull(module);
        Assert.Equal("email", module.ModuleKey);
        Assert.Equal("Email Automation", module.Name);
        Assert.Equal("1.0.0", module.Version);
        Assert.Equal("email", module.ConfigurationNamespace);
        Assert.False(module.GlobalEnabled);
    }

    [Fact]
    public async Task EmailModule_Seed_IsIdempotent_DoesNotDuplicate()
    {
        // Register once
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey,
            EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion,
            EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);

        // Second registration should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _registry.RegisterModuleAsync(
                EmailModuleKeys.ModuleKey,
                EmailModuleKeys.ModuleName,
                EmailModuleKeys.ModuleVersion,
                EmailModuleKeys.ModuleDescription,
                EmailModuleKeys.ConfigurationNamespace));

        // Only one module registered
        var modules = await _registry.GetModulesAsync();
        Assert.Single(modules);
    }

    [Fact]
    public async Task EmailModule_GlobalEnable_SetsGlobalEnabledTrue()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion, EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);

        await _registry.EnableModuleAsync(EmailModuleKeys.ModuleKey);

        var module = await _registry.GetModuleAsync(EmailModuleKeys.ModuleKey);
        Assert.True(module!.GlobalEnabled);
    }

    [Fact]
    public async Task EmailModule_GlobalDisable_SetsGlobalEnabledFalse()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion, EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);
        await _registry.EnableModuleAsync(EmailModuleKeys.ModuleKey);

        await _registry.DisableModuleAsync(EmailModuleKeys.ModuleKey);

        var module = await _registry.GetModuleAsync(EmailModuleKeys.ModuleKey);
        Assert.False(module!.GlobalEnabled);
    }

    [Fact]
    public async Task EmailModule_TenantEnable_CreatesOrUpdatesRecord()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion, EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);

        var tenantId = Guid.NewGuid();
        await _registry.EnableModuleForTenantAsync(tenantId, EmailModuleKeys.ModuleKey);

        var tenantModules = await _registry.GetTenantModulesAsync(tenantId);
        var emailModule = tenantModules.FirstOrDefault(m => m.ModuleKey == EmailModuleKeys.ModuleKey);

        Assert.NotNull(emailModule);
        Assert.True(emailModule.Enabled);
    }

    [Fact]
    public async Task EmailModule_EffectiveEnabled_RequiresBothGlobalAndTenantEnabled()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion, EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);

        var tenantId = Guid.NewGuid();

        // Global disabled, tenant enabled → effective = false
        await _registry.EnableModuleForTenantAsync(tenantId, EmailModuleKeys.ModuleKey);
        var global = await _registry.GetModuleAsync(EmailModuleKeys.ModuleKey);
        var tenantModules = await _registry.GetTenantModulesAsync(tenantId);
        var tenant = tenantModules.FirstOrDefault(m => m.ModuleKey == EmailModuleKeys.ModuleKey);
        var effective = Xenia.Application.Modules.EffectiveModuleDto.From(global!, tenant);

        Assert.False(global!.GlobalEnabled);
        Assert.True(tenant!.Enabled);
        Assert.False(effective.EffectiveEnabled);

        // Global enabled, tenant enabled → effective = true
        await _registry.EnableModuleAsync(EmailModuleKeys.ModuleKey);
        global = await _registry.GetModuleAsync(EmailModuleKeys.ModuleKey);
        effective = Xenia.Application.Modules.EffectiveModuleDto.From(global!, tenant);

        Assert.True(effective.EffectiveEnabled);
    }

    [Fact]
    public async Task EmailModule_MissingTenantOverride_EffectiveEnabledFalse()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion, EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);
        await _registry.EnableModuleAsync(EmailModuleKeys.ModuleKey);

        var tenantId = Guid.NewGuid();
        var global = await _registry.GetModuleAsync(EmailModuleKeys.ModuleKey);
        var effective = Xenia.Application.Modules.EffectiveModuleDto.From(global!, tenant: null);

        // No tenant override → TenantEnabled defaults to false → effective = false
        Assert.True(global!.GlobalEnabled);
        Assert.False(effective.TenantEnabled);
        Assert.False(effective.EffectiveEnabled);
    }

    [Fact]
    public async Task EmailModule_TenantIsolation_TenantACannotSeeTenantBModules()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion, EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _registry.EnableModuleForTenantAsync(tenantA, EmailModuleKeys.ModuleKey);

        var tenantBModules = await _registry.GetTenantModulesAsync(tenantB);
        var emailModuleForB = tenantBModules.FirstOrDefault(m => m.ModuleKey == EmailModuleKeys.ModuleKey);

        Assert.Null(emailModuleForB);
    }

    [Fact]
    public async Task EmailModule_GloballyDisabled_TenantCannotEffectivelyEnable()
    {
        await _registry.RegisterModuleAsync(
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleName,
            EmailModuleKeys.ModuleVersion, EmailModuleKeys.ModuleDescription,
            EmailModuleKeys.ConfigurationNamespace);
        // Do NOT enable globally

        var tenantId = Guid.NewGuid();
        await _registry.EnableModuleForTenantAsync(tenantId, EmailModuleKeys.ModuleKey);

        var global = await _registry.GetModuleAsync(EmailModuleKeys.ModuleKey);
        var tenantModules = await _registry.GetTenantModulesAsync(tenantId);
        var tenant = tenantModules.FirstOrDefault(m => m.ModuleKey == EmailModuleKeys.ModuleKey);
        var effective = Xenia.Application.Modules.EffectiveModuleDto.From(global!, tenant);

        Assert.False(global!.GlobalEnabled);
        Assert.True(tenant!.Enabled);
        Assert.False(effective.EffectiveEnabled, "Globally disabled module cannot be effectively enabled by tenant.");
    }

    public void Dispose() => _db.Dispose();
}
