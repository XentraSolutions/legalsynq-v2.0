using BuildingBlocks.Commerce;
using BuildingBlocks.Exceptions;
using Contracts.Commerce;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tenant.Application.Configuration;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Application.Services;
using Tenant.Domain;
using Xunit;

namespace Tenant.Application.Tests;

/// <summary>
/// Unit tests for timezone-related behavior in <see cref="TenantService"/>:
///   - Default timezone seeded on ProvisionAsync
///   - Default timezone seeded on CreateAsync (when caller omits it)
///   - UpdateTimezoneAsync happy path
///   - UpdateTimezoneAsync rejects an unrecognized timezone string
/// </summary>
public class TenantServiceTimezoneTests
{
    // ── ProvisionAsync seeds default timezone ─────────────────────────────────

    [Fact]
    public async Task ProvisionAsync_SetsDefaultTimezone()
    {
        var repo    = new CapturingTenantRepository();
        var service = BuildService(repo);

        await service.ProvisionAsync(new("Acme Corp", "acme"), default);

        Assert.NotNull(repo.Added);
        Assert.Equal(TenantDefaults.Timezone, repo.Added!.TimeZone);
    }

    // ── CreateAsync seeds default timezone when caller omits it ──────────────

    [Fact]
    public async Task CreateAsync_WhenNoTimezoneProvided_SetsDefault()
    {
        var repo    = new CapturingTenantRepository();
        var service = BuildService(repo);

        await service.CreateAsync(new CreateTenantRequest(
            Code:        "beta",
            DisplayName: "Beta Inc"), default);

        Assert.NotNull(repo.Added);
        Assert.Equal(TenantDefaults.Timezone, repo.Added!.TimeZone);
    }

    // ── CreateAsync respects explicit timezone when caller provides one ───────

    [Fact]
    public async Task CreateAsync_WhenTimezoneProvided_UsesIt()
    {
        var repo    = new CapturingTenantRepository();
        var service = BuildService(repo);

        await service.CreateAsync(new CreateTenantRequest(
            Code:        "gamma",
            DisplayName: "Gamma LLC",
            TimeZone:    "America/New_York"), default);

        Assert.NotNull(repo.Added);
        Assert.Equal("America/New_York", repo.Added!.TimeZone);
    }

    // ── UpdateTimezoneAsync happy path ────────────────────────────────────────

    [Fact]
    public async Task UpdateTimezoneAsync_ValidTimezone_UpdatesAndReturnsValue()
    {
        var tenant  = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var repo    = new CapturingTenantRepository(existing: tenant);
        var settings = new CapturingSettingRepository();
        var service = BuildService(repo, settings);

        var result = await service.UpdateTimezoneAsync(tenant.Id, "America/Chicago", default);

        Assert.Equal("America/Chicago", result);
        Assert.True(repo.Updated);
        Assert.Equal("America/Chicago", tenant.TimeZone);
        Assert.NotNull(settings.Added);
        Assert.Equal(TenantDefaults.TimezoneSettingKey, settings.Added!.SettingKey);
        Assert.Equal("America/Chicago", settings.Added.SettingValue);
    }

    // ── UpdateTimezoneAsync rejects unrecognized timezone ─────────────────────

    [Fact]
    public async Task UpdateTimezoneAsync_InvalidTimezone_ThrowsValidationException()
    {
        var tenant  = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var repo    = new CapturingTenantRepository(existing: tenant);
        var service = BuildService(repo, new CapturingSettingRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateTimezoneAsync(tenant.Id, "Not/A/Timezone", default));
    }

    // ── UpdateTimezoneAsync rejects unknown tenant ────────────────────────────

    [Fact]
    public async Task UpdateTimezoneAsync_UnknownTenant_ThrowsNotFoundException()
    {
        var repo    = new CapturingTenantRepository(existing: null);
        var service = BuildService(repo, new CapturingSettingRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateTimezoneAsync(Guid.NewGuid(), "America/Los_Angeles", default));
    }

    [Fact]
    public async Task GetTimezoneAsync_WhenTenantTimezoneMissing_FallsBackToCanonicalSetting()
    {
        var tenant    = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var repo      = new CapturingTenantRepository(existing: tenant);
        var settings  = new CapturingSettingRepository(canonicalTimezone: MakeSetting(tenant.Id, TenantDefaults.TimezoneSettingKey, "UTC"));
        var service   = BuildService(repo, settings);

        var result = await service.GetTimezoneAsync(tenant.Id, default);

        Assert.Equal("UTC", result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TenantService BuildService(ITenantRepository repo) =>
        BuildService(repo, new CapturingSettingRepository());

    private static TenantService BuildService(ITenantRepository repo, ISettingRepository settings) =>
        new(
            repo,
            settings,
            Options.Create(new PlatformRoutingOptions()),
            new NoOpCommerceNotifier(),
            NullLogger<TenantService>.Instance);

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class CapturingTenantRepository : ITenantRepository
    {
        private readonly Domain.Tenant? _existing;

        public Domain.Tenant? Added   { get; private set; }
        public bool           Updated { get; private set; }

        public CapturingTenantRepository(Domain.Tenant? existing = null) => _existing = existing;

        public Task<Domain.Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_existing);

        public Task<Domain.Tenant?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult<Domain.Tenant?>(null);

        public Task<Domain.Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default)
            => Task.FromResult<Domain.Tenant?>(null);

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<bool> ExistsBySubdomainAsync(string subdomain, Guid? excludeId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<(List<Domain.Tenant> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult((new List<Domain.Tenant>(), 0));

        public Task AddAsync(Domain.Tenant tenant, CancellationToken ct = default)
        {
            Added = tenant;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Domain.Tenant tenant, CancellationToken ct = default)
        {
            Updated = true;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingSettingRepository : ISettingRepository
    {
        private readonly TenantSetting? _canonicalTimezone;
        private readonly TenantSetting? _legacyTimezone;

        public TenantSetting? Added { get; private set; }

        public CapturingSettingRepository(
            TenantSetting? canonicalTimezone = null,
            TenantSetting? legacyTimezone = null)
        {
            _canonicalTimezone = canonicalTimezone;
            _legacyTimezone = legacyTimezone;
        }

        public Task<TenantSetting?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<TenantSetting?>(null);

        public Task<List<TenantSetting>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new List<TenantSetting>());

        public Task<TenantSetting?> GetByKeyAsync(Guid tenantId, string settingKey, string? productKey, CancellationToken ct = default)
        {
            TenantSetting? result = settingKey switch
            {
                TenantDefaults.TimezoneSettingKey => _canonicalTimezone,
                TenantDefaults.LegacyTimezoneSettingKey => _legacyTimezone,
                _ => null
            };
            return Task.FromResult(result);
        }

        public Task AddAsync(TenantSetting setting, CancellationToken ct = default)
        {
            Added = setting;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TenantSetting setting, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteAsync(TenantSetting setting, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static TenantSetting MakeSetting(Guid tenantId, string key, string value)
        => TenantSetting.Create(tenantId, key, value, SettingValueType.String);

    private sealed class NoOpCommerceNotifier : ICommerceLifecycleNotifier
    {
        public Task NotifyAsync(CommerceLifecycleEvent ev, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
