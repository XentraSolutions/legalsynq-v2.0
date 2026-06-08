using BuildingBlocks.Exceptions;
using Tenant.Application.Services;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Xunit;

namespace Tenant.Application.Tests;

public class CareConnectAccessCodeServiceTests
{
    [Fact]
    public async Task SetAsync_CreatesHashAndVersionOne()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var settings = new StubSettingRepository();
        var service = BuildService(tenant, settings);

        var result = await service.SetAsync(tenant.Id, " Password123 ");

        Assert.True(result.Configured);
        Assert.Equal(1, result.Version);
        Assert.Equal("Password123", result.RevealedCode);

        var hash = await settings.GetByKeyAsync(tenant.Id, "careconnect.public-network.access-code.hash", "careconnect");
        var version = await settings.GetByKeyAsync(tenant.Id, "careconnect.public-network.access-code.version", "careconnect");

        Assert.NotNull(hash);
        Assert.NotEqual("Password123", hash!.SettingValue);
        Assert.NotNull(version);
        Assert.Equal("1", version!.SettingValue);
    }

    [Fact]
    public async Task SetAsync_ReplacingCode_UpdatesHashAndIncrementsVersion()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var settings = new StubSettingRepository();
        var service = BuildService(tenant, settings);

        var first = await service.SetAsync(tenant.Id, "Password123");
        var second = await service.SetAsync(tenant.Id, "Password456");

        var hash = await settings.GetByKeyAsync(tenant.Id, "careconnect.public-network.access-code.hash", "careconnect");
        var version = await settings.GetByKeyAsync(tenant.Id, "careconnect.public-network.access-code.version", "careconnect");

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.NotNull(hash);
        Assert.NotEqual("Password456", hash!.SettingValue);
        Assert.NotNull(version);
        Assert.Equal("2", version!.SettingValue);
    }

    [Fact]
    public async Task ClearAsync_RemovesHashAndIncrementsVersion()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var settings = new StubSettingRepository();
        var service = BuildService(tenant, settings);

        await service.SetAsync(tenant.Id, "Password123");
        var result = await service.ClearAsync(tenant.Id);

        Assert.False(result.Configured);
        Assert.Equal(2, result.Version);
        Assert.Null(result.UpdatedAtUtc);
        Assert.Null(await settings.GetByKeyAsync(tenant.Id, "careconnect.public-network.access-code.hash", "careconnect"));

        var version = await settings.GetByKeyAsync(tenant.Id, "careconnect.public-network.access-code.version", "careconnect");
        Assert.NotNull(version);
        Assert.Equal("2", version!.SettingValue);
    }

    [Fact]
    public async Task GetMetadataAsync_NeverReturnsHash()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var settings = new StubSettingRepository();
        var service = BuildService(tenant, settings);

        await service.SetAsync(tenant.Id, "Password123");
        var metadata = await service.GetMetadataAsync(tenant.Id);

        Assert.True(metadata.Configured);
        Assert.Equal(1, metadata.Version);
        Assert.NotNull(metadata.UpdatedAtUtc);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsTrue_ForCorrectCode()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var service = BuildService(tenant, new StubSettingRepository());

        await service.SetAsync(tenant.Id, "Password123");
        var result = await service.VerifyAsync(tenant.Id, " Password123 ");

        Assert.True(result.Ok);
        Assert.True(result.Configured);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFalse_ForIncorrectCode()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var service = BuildService(tenant, new StubSettingRepository());

        await service.SetAsync(tenant.Id, "Password123");
        var result = await service.VerifyAsync(tenant.Id, "Password999");

        Assert.False(result.Ok);
        Assert.True(result.Configured);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task StatusAndVerify_ReturnConfiguredFalse_WhenUnconfigured()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var service = BuildService(tenant, new StubSettingRepository());

        var status = await service.GetStatusAsync(tenant.Id);
        var verify = await service.VerifyAsync(tenant.Id, "Password123");

        Assert.False(status.Configured);
        Assert.Equal(0, status.Version);
        Assert.False(verify.Ok);
        Assert.False(verify.Configured);
        Assert.Equal(0, verify.Version);
    }

    [Fact]
    public async Task SetAsync_RejectsInvalidLength()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme");
        var service = BuildService(tenant, new StubSettingRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.SetAsync(tenant.Id, "short"));
    }

    private static CareConnectAccessCodeService BuildService(Domain.Tenant tenant, ISettingRepository settings)
        => new(settings, new StubTenantRepository(tenant));

    private sealed class StubTenantRepository : ITenantRepository
    {
        private readonly Domain.Tenant? _tenant;

        public StubTenantRepository(Domain.Tenant? tenant) => _tenant = tenant;

        public Task<Domain.Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_tenant?.Id == id ? _tenant : null);

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

        public Task AddAsync(Domain.Tenant tenant, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Domain.Tenant tenant, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubSettingRepository : ISettingRepository
    {
        private readonly List<TenantSetting> _settings = [];

        public Task<TenantSetting?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_settings.FirstOrDefault(s => s.Id == id));

        public Task<List<TenantSetting>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_settings.Where(s => s.TenantId == tenantId).ToList());

        public Task<TenantSetting?> GetByKeyAsync(Guid tenantId, string settingKey, string? productKey, CancellationToken ct = default)
            => Task.FromResult(_settings.FirstOrDefault(s =>
                s.TenantId == tenantId &&
                s.SettingKey == settingKey &&
                s.ProductKey == productKey));

        public Task AddAsync(TenantSetting setting, CancellationToken ct = default)
        {
            _settings.Add(setting);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TenantSetting setting, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteAsync(TenantSetting setting, CancellationToken ct = default)
        {
            _settings.Remove(setting);
            return Task.CompletedTask;
        }
    }
}
