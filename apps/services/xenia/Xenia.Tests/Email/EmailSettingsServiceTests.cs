using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.Email;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Persistence;
using Xenia.Infrastructure.Platform;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for EfEmailSettingsService — get-or-create, update, validation, versioning.
/// Uses InMemory database — no MySQL required.
/// </summary>
public sealed class EmailSettingsServiceTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfEmailSettingsService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public EmailSettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new XeniaDbContext(options);

        var auditAdapter = new UnavailableAuditAdapter(
            NullLogger<UnavailableAuditAdapter>.Instance);

        _service = new EfEmailSettingsService(
            _db,
            auditAdapter,
            NullLogger<EfEmailSettingsService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── GetOrCreate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreate_NoExistingRecord_CreatesDefaults()
    {
        var settings = await _service.GetOrCreateAsync(_tenantId);

        Assert.NotNull(settings);
        Assert.Equal(_tenantId, settings.TenantId);
        Assert.Equal(30, settings.ConnectionTimeoutSeconds);
        Assert.Equal(2, settings.ValidationRetryLimit);
        Assert.Equal(90, settings.ValidationHistoryRetentionDays);
        Assert.True(settings.RequireTls);
        Assert.False(settings.AllowCustomHosts);
        Assert.Equal("Strict", settings.SsrfPolicyMode);
        Assert.Equal(0, settings.Version);
    }

    [Fact]
    public async Task GetOrCreate_ExistingRecord_ReturnsExisting()
    {
        var first = await _service.GetOrCreateAsync(_tenantId);
        var second = await _service.GetOrCreateAsync(_tenantId);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _db.EmailSettings.CountAsync());
    }

    [Fact]
    public async Task GetOrCreate_DifferentTenants_CreatesSeparateRecords()
    {
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();

        await _service.GetOrCreateAsync(t1);
        await _service.GetOrCreateAsync(t2);

        Assert.Equal(2, await _db.EmailSettings.CountAsync());
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_UpdatesFields()
    {
        var initial = await _service.GetOrCreateAsync(_tenantId);

        var updated = await _service.UpdateAsync(_tenantId, null, new UpdateEmailSettingsRequest
        {
            ConnectionTimeoutSeconds = 60,
            ValidationRetryLimit = 3,
            RequireTls = true,
            SsrfPolicyMode = "Strict",
            ExpectedVersion = 0,
        });

        Assert.Equal(60, updated.ConnectionTimeoutSeconds);
        Assert.Equal(3, updated.ValidationRetryLimit);
        Assert.Equal(1, updated.Version);
    }

    [Fact]
    public async Task Update_ConcurrencyConflict_Throws()
    {
        await _service.GetOrCreateAsync(_tenantId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAsync(_tenantId, null, new UpdateEmailSettingsRequest
            {
                ExpectedVersion = 99, // wrong version
            }));
    }

    [Fact]
    public async Task Update_InvalidConnectionTimeout_Throws()
    {
        await _service.GetOrCreateAsync(_tenantId);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.UpdateAsync(_tenantId, null, new UpdateEmailSettingsRequest
            {
                ConnectionTimeoutSeconds = 200, // > 120
                ExpectedVersion = 0,
            }));
    }

    [Fact]
    public async Task Update_InvalidRetryLimit_Throws()
    {
        await _service.GetOrCreateAsync(_tenantId);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.UpdateAsync(_tenantId, null, new UpdateEmailSettingsRequest
            {
                ValidationRetryLimit = 10, // > 5
                ExpectedVersion = 0,
            }));
    }

    [Fact]
    public async Task Update_InvalidSsrfPolicyMode_Throws()
    {
        await _service.GetOrCreateAsync(_tenantId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(_tenantId, null, new UpdateEmailSettingsRequest
            {
                SsrfPolicyMode = "Unknown",
                ExpectedVersion = 0,
            }));
    }

    [Fact]
    public async Task Update_InvalidPort_Throws()
    {
        await _service.GetOrCreateAsync(_tenantId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(_tenantId, null, new UpdateEmailSettingsRequest
            {
                AllowedPorts = "993,99999", // 99999 > 65535
                ExpectedVersion = 0,
            }));
    }

    [Fact]
    public async Task Update_NoExistingRecord_CreatesAndUpdates()
    {
        // Should create defaults first, then apply update
        var result = await _service.UpdateAsync(_tenantId, null, new UpdateEmailSettingsRequest
        {
            ConnectionTimeoutSeconds = 45,
            ExpectedVersion = 0,
        });

        Assert.Equal(45, result.ConnectionTimeoutSeconds);
        Assert.Equal(1, await _db.EmailSettings.CountAsync());
    }

    // ── GetAllowedPortsList ───────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreate_DefaultAllowedPorts_ParsesCorrectly()
    {
        var settings = await _service.GetOrCreateAsync(_tenantId);
        var entity = await _db.EmailSettings.FirstAsync(s => s.TenantId == _tenantId);
        var ports = entity.GetAllowedPortsList();

        Assert.Contains(993, ports);
        Assert.Contains(995, ports);
        Assert.Contains(443, ports);
    }
}
