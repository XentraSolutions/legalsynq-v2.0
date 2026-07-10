using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Infrastructure.Platform;
using Xenia.Infrastructure.Registry;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Registry;

/// <summary>
/// Unit tests for adapter registry and noop adapter implementations.
///
/// Verifies: adapters report unavailable when unconfigured,
/// all eight adapter types are registered, and the audit adapter
/// falls back to logging rather than discarding events.
/// </summary>
public sealed class AdapterRegistryTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfAdapterRegistry _registry;

    public AdapterRegistryTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new XeniaDbContext(options);
        _registry = new EfAdapterRegistry(_db, NullLogger<EfAdapterRegistry>.Instance);
    }

    [Fact]
    public async Task GetAll_SeedsAllEightAdapters()
    {
        var adapters = await _registry.GetAllAsync();

        Assert.Equal(8, adapters.Count);
        Assert.Contains(adapters, a => a.AdapterKey == "tenant");
        Assert.Contains(adapters, a => a.AdapterKey == "identity");
        Assert.Contains(adapters, a => a.AdapterKey == "document");
        Assert.Contains(adapters, a => a.AdapterKey == "audit");
        Assert.Contains(adapters, a => a.AdapterKey == "notification");
        Assert.Contains(adapters, a => a.AdapterKey == "storage");
        Assert.Contains(adapters, a => a.AdapterKey == "workflow");
        Assert.Contains(adapters, a => a.AdapterKey == "ai");
    }

    [Fact]
    public async Task GetAll_AllAdaptersInitiallyUnconfigured()
    {
        var adapters = await _registry.GetAllAsync();

        foreach (var a in adapters)
        {
            Assert.Equal("Unconfigured", a.ConfigurationStatus);
        }
    }

    [Fact]
    public void UnavailableTenantAdapter_IsConfigured_ReturnsFalse()
    {
        var adapter = new UnavailableTenantAdapter();
        Assert.False(adapter.IsConfigured);
    }

    [Fact]
    public async Task UnavailableTenantAdapter_Validate_ReturnsNotAvailable()
    {
        var adapter = new UnavailableTenantAdapter();
        var result = await adapter.ValidateTenantAsync(Guid.CreateVersion7());

        Assert.False(result.IsValid);
        Assert.False(result.IsAvailable);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task UnavailableAuditAdapter_RecordEvent_DoesNotThrow()
    {
        var adapter = new UnavailableAuditAdapter(NullLogger<UnavailableAuditAdapter>.Instance);

        var evt = new XeniaAuditEvent
        {
            Action = "xenia.module.enabled",
            ResourceType = "module",
            ResourceId = "xenia.test",
            Result = "success",
            TenantId = Guid.CreateVersion7(),
            ActorId = Guid.CreateVersion7(),
            CorrelationId = "test-correlation-id",
            OccurredAt = DateTime.UtcNow,
        };

        // Must not throw — falls back to structured log
        var ex = await Record.ExceptionAsync(() => adapter.RecordEventAsync(evt));
        Assert.Null(ex);
    }

    [Fact]
    public void AllUnavailableAdapters_IsConfigured_ReturnsFalse()
    {
        var adapters = new object[]
        {
            new UnavailableTenantAdapter(),
            new UnavailableIdentityAdapter(),
            new UnavailableDocumentAdapter(),
            new UnavailableAuditAdapter(NullLogger<UnavailableAuditAdapter>.Instance),
            new UnavailableNotificationAdapter(),
            new UnavailableStorageAdapter(),
            new UnavailableWorkflowAdapter(),
            new UnavailableAiAdapter(),
        };

        foreach (var adapter in adapters)
        {
            var isConfigured = adapter.GetType().GetProperty("IsConfigured")!.GetValue(adapter);
            Assert.False((bool)isConfigured!, $"{adapter.GetType().Name} should not be configured by default.");
        }
    }

    public void Dispose() => _db.Dispose();
}
