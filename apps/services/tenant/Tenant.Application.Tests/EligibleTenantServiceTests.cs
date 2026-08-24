using Tenant.Application.Interfaces;
using Tenant.Application.Services;
using Tenant.Domain;
using Xunit;

namespace Tenant.Application.Tests;

public sealed class EligibleTenantServiceTests
{
    [Fact]
    public async Task Uses_all_supported_SynqLiens_product_keys_and_current_utc_time()
    {
        var now = new DateTimeOffset(2026, 8, 17, 11, 0, 0, TimeSpan.Zero);
        var expectedTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repository = new CapturingTenantRepository([expectedTenantId]);
        var service = new EligibleTenantService(repository, new FixedTimeProvider(now));

        var result = await service.ListActiveSynqLiensTenantIdsAsync();

        Assert.Equal([expectedTenantId], result);
        Assert.Equal(now.UtcDateTime, repository.UtcNow);
        Assert.Equal(
            new[] { ProductKeys.Liens, "synq_liens", "synqliens", "synqlien" },
            repository.ProductKeys);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturingTenantRepository(List<Guid> tenantIds) : IEligibleTenantRepository
    {
        public IReadOnlyCollection<string> ProductKeys { get; private set; } = [];
        public DateTime UtcNow { get; private set; }

        public Task<List<Guid>> ListActiveTenantIdsByProductKeysAsync(
            IReadOnlyCollection<string> productKeys,
            DateTime utcNow,
            CancellationToken ct = default)
        {
            ProductKeys = productKeys;
            UtcNow = utcNow;
            return Task.FromResult(tenantIds);
        }
    }
}
