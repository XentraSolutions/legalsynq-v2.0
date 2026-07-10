using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Infrastructure.Modules;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Modules;

/// <summary>
/// Unit tests for the Xenia module registry using an in-memory database.
///
/// These tests verify: registration, duplicate rejection, enable/disable toggling,
/// and retrieval — without requiring a real MySQL instance.
/// </summary>
public sealed class ModuleRegistryTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfModuleRegistry _registry;

    public ModuleRegistryTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new XeniaDbContext(options);
        _registry = new EfModuleRegistry(_db, NullLogger<EfModuleRegistry>.Instance);
    }

    [Fact]
    public async Task RegisterModule_NewModule_Succeeds()
    {
        await _registry.RegisterModuleAsync(
            moduleKey: "xenia.test",
            name: "Test Module",
            version: "1.0.0",
            description: "A test module",
            configurationNamespace: "xenia.test");

        var modules = await _registry.GetModulesAsync();

        Assert.Single(modules);
        Assert.Equal("xenia.test", modules[0].ModuleKey);
        Assert.Equal("Test Module", modules[0].Name);
        Assert.Equal("1.0.0", modules[0].Version);
        Assert.False(modules[0].GlobalEnabled);
    }

    [Fact]
    public async Task RegisterModule_DuplicateKey_ThrowsInvalidOperationException()
    {
        await _registry.RegisterModuleAsync("xenia.dupe", "Duplicate", "1.0.0", "", "xenia.dupe");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _registry.RegisterModuleAsync("xenia.dupe", "Duplicate Again", "2.0.0", "", "xenia.dupe"));
    }

    [Fact]
    public async Task EnableModule_ExistingModule_SetsGlobalEnabledTrue()
    {
        await _registry.RegisterModuleAsync("xenia.enable-test", "Enable Test", "1.0.0", "", "xenia.enable-test");

        await _registry.EnableModuleAsync("xenia.enable-test");

        var module = await _registry.GetModuleAsync("xenia.enable-test");
        Assert.NotNull(module);
        Assert.True(module.GlobalEnabled);
    }

    [Fact]
    public async Task DisableModule_ExistingModule_SetsGlobalEnabledFalse()
    {
        await _registry.RegisterModuleAsync("xenia.disable-test", "Disable Test", "1.0.0", "", "xenia.disable-test");
        await _registry.EnableModuleAsync("xenia.disable-test");

        await _registry.DisableModuleAsync("xenia.disable-test");

        var module = await _registry.GetModuleAsync("xenia.disable-test");
        Assert.NotNull(module);
        Assert.False(module.GlobalEnabled);
    }

    [Fact]
    public async Task GetModules_ReturnsAllRegistered()
    {
        await _registry.RegisterModuleAsync("xenia.alpha", "Alpha", "1.0.0", "", "xenia.alpha");
        await _registry.RegisterModuleAsync("xenia.beta", "Beta", "1.0.0", "", "xenia.beta");
        await _registry.RegisterModuleAsync("xenia.gamma", "Gamma", "1.0.0", "", "xenia.gamma");

        var modules = await _registry.GetModulesAsync();

        Assert.Equal(3, modules.Count);
    }

    [Fact]
    public async Task GetModule_NonExistentKey_ReturnsNull()
    {
        var module = await _registry.GetModuleAsync("xenia.does-not-exist");
        Assert.Null(module);
    }

    public void Dispose() => _db.Dispose();
}
