using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Domain.Adapters;
using Xenia.Infrastructure.Persistence;
using Xenia.Infrastructure.Registry;
using Xunit;

namespace Xenia.Tests.Registry;

/// <summary>
/// Tests for adapter criticality classification and readiness semantics.
///
/// Validates:
/// - Exactly 8 generic adapters are seeded (no Lien, no CareConnect)
/// - Tenant and Identity adapters are Mandatory
/// - Document/Audit/Notification/Storage/Workflow/AI adapters are Optional
/// - Lien adapter is absent
/// - CareConnect adapter is absent
/// - Storage adapter is present with correct criticality
/// - AI adapter is present with correct criticality
/// - Seed is idempotent (running twice produces identical results)
/// - Adapter count does not change on re-seed
/// </summary>
public sealed class AdapterCriticalityTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfAdapterRegistry _registry;

    public AdapterCriticalityTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new XeniaDbContext(options);
        _registry = new EfAdapterRegistry(_db, NullLogger<EfAdapterRegistry>.Instance);
    }

    // ── Adapter boundary ───────────────────────────────────────────────────────

    [Fact]
    public async Task AdapterBoundary_ExactlyEightAdapters()
    {
        var adapters = await _registry.GetAllAsync();
        Assert.Equal(8, adapters.Count);
    }

    [Fact]
    public async Task AdapterBoundary_LienAdapter_IsAbsent()
    {
        var adapters = await _registry.GetAllAsync();
        Assert.DoesNotContain(adapters, a =>
            a.AdapterKey.Contains("lien", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdapterBoundary_CareConnectAdapter_IsAbsent()
    {
        var adapters = await _registry.GetAllAsync();
        Assert.DoesNotContain(adapters, a =>
            a.AdapterKey.Contains("careconnect", StringComparison.OrdinalIgnoreCase) ||
            a.AdapterKey.Contains("care_connect", StringComparison.OrdinalIgnoreCase) ||
            a.AdapterKey.Contains("care-connect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdapterBoundary_StorageAdapter_IsPresent()
    {
        var adapters = await _registry.GetAllAsync();
        Assert.Contains(adapters, a => a.AdapterKey == "storage");
    }

    [Fact]
    public async Task AdapterBoundary_AiAdapter_IsPresent()
    {
        var adapters = await _registry.GetAllAsync();
        Assert.Contains(adapters, a => a.AdapterKey == "ai");
    }

    [Fact]
    public async Task AdapterBoundary_AllExpectedKeys_Present()
    {
        var adapters = await _registry.GetAllAsync();
        var keys = adapters.Select(a => a.AdapterKey).ToHashSet();

        Assert.Contains("tenant", keys);
        Assert.Contains("identity", keys);
        Assert.Contains("document", keys);
        Assert.Contains("audit", keys);
        Assert.Contains("notification", keys);
        Assert.Contains("storage", keys);
        Assert.Contains("workflow", keys);
        Assert.Contains("ai", keys);
    }

    // ── Criticality classification ─────────────────────────────────────────────

    [Fact]
    public async Task Criticality_TenantAdapter_IsMandatory()
    {
        var adapters = await _registry.GetAllAsync();
        var tenant = adapters.Single(a => a.AdapterKey == "tenant");
        Assert.Equal("Mandatory", tenant.Criticality);
    }

    [Fact]
    public async Task Criticality_IdentityAdapter_IsMandatory()
    {
        var adapters = await _registry.GetAllAsync();
        var identity = adapters.Single(a => a.AdapterKey == "identity");
        Assert.Equal("Mandatory", identity.Criticality);
    }

    [Fact]
    public async Task Criticality_DocumentAdapter_IsOptional()
    {
        var adapters = await _registry.GetAllAsync();
        var doc = adapters.Single(a => a.AdapterKey == "document");
        Assert.Equal("Optional", doc.Criticality);
    }

    [Fact]
    public async Task Criticality_AuditAdapter_IsOptional()
    {
        var adapters = await _registry.GetAllAsync();
        var audit = adapters.Single(a => a.AdapterKey == "audit");
        Assert.Equal("Optional", audit.Criticality);
    }

    [Fact]
    public async Task Criticality_NotificationAdapter_IsOptional()
    {
        var adapters = await _registry.GetAllAsync();
        var notif = adapters.Single(a => a.AdapterKey == "notification");
        Assert.Equal("Optional", notif.Criticality);
    }

    [Fact]
    public async Task Criticality_StorageAdapter_IsOptional()
    {
        var adapters = await _registry.GetAllAsync();
        var storage = adapters.Single(a => a.AdapterKey == "storage");
        Assert.Equal("Optional", storage.Criticality);
    }

    [Fact]
    public async Task Criticality_WorkflowAdapter_IsOptional()
    {
        var adapters = await _registry.GetAllAsync();
        var workflow = adapters.Single(a => a.AdapterKey == "workflow");
        Assert.Equal("Optional", workflow.Criticality);
    }

    [Fact]
    public async Task Criticality_AiAdapter_IsOptional()
    {
        var adapters = await _registry.GetAllAsync();
        var ai = adapters.Single(a => a.AdapterKey == "ai");
        Assert.Equal("Optional", ai.Criticality);
    }

    // ── Seed idempotency ───────────────────────────────────────────────────────

    [Fact]
    public async Task SeedIdempotency_RunTwice_CountStaysAtEight()
    {
        // First seed (via GetAllAsync)
        var first = await _registry.GetAllAsync();
        Assert.Equal(8, first.Count);

        // Second seed — must not duplicate
        var second = await _registry.GetAllAsync();
        Assert.Equal(8, second.Count);
    }

    [Fact]
    public async Task SeedIdempotency_RunTenTimes_CountStaysAtEight()
    {
        for (var i = 0; i < 10; i++)
            _ = await _registry.GetAllAsync();

        var final = await _registry.GetAllAsync();
        Assert.Equal(8, final.Count);
    }

    // ── Domain enum completeness ───────────────────────────────────────────────

    [Fact]
    public void AdapterType_EnumHasExactlyEightValues()
    {
        var values = Enum.GetValues<AdapterType>();
        Assert.Equal(8, values.Length);
    }

    [Fact]
    public void AdapterType_NoLienValue()
    {
        var names = Enum.GetNames<AdapterType>();
        Assert.DoesNotContain(names, n => n.Contains("Lien", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AdapterType_NoCareConnectValue()
    {
        var names = Enum.GetNames<AdapterType>();
        Assert.DoesNotContain(names, n => n.Contains("CareConnect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AdapterCriticality_EnumHasThreeValues()
    {
        var values = Enum.GetValues<AdapterCriticality>();
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void AdapterCriticality_HasMandatory()
        => Assert.True(Enum.IsDefined(typeof(AdapterCriticality), AdapterCriticality.Mandatory));

    [Fact]
    public void AdapterCriticality_HasOptional()
        => Assert.True(Enum.IsDefined(typeof(AdapterCriticality), AdapterCriticality.Optional));

    [Fact]
    public void AdapterCriticality_HasDisabled()
        => Assert.True(Enum.IsDefined(typeof(AdapterCriticality), AdapterCriticality.Disabled));

    public void Dispose() => _db.Dispose();
}
