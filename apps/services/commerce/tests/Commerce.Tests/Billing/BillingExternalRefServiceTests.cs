using Xunit;
using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.Billing;

public sealed class BillingExternalRefServiceTests
{
    private static async Task<Guid> SeedAccountAsync(BillingTestHost host)
    {
        var a = await host.AccountService.CreateAsync(
            new CreateBillingAccountRequest("Acme", null, "USD"), default);
        return a.Id;
    }

    [Fact]
    public async Task Add_normalizes_HostPlatformKey_and_persists()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);

        var r = await host.ExternalRefService.AddAsync(id,
            new CreateExternalRefRequest("LegalSynq", "tenant-1", null, true), default);

        Assert.Equal("legalsynq", r.HostPlatformKey);
        Assert.True(r.IsPrimary);
    }

    [Fact]
    public async Task Add_duplicate_HostPlatformKey_plus_TenantId_throws_DuplicateKey()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);

        await host.ExternalRefService.AddAsync(id,
            new CreateExternalRefRequest("legalsynq", "tenant-1", null, true), default);
        await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            host.ExternalRefService.AddAsync(id,
                new CreateExternalRefRequest("LEGALSYNQ", "tenant-1", null, false), default));
    }

    [Fact]
    public async Task MakePrimary_demotes_previous_primary()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);

        var first = await host.ExternalRefService.AddAsync(id,
            new CreateExternalRefRequest("legalsynq", "tenant-1", null, true), default);
        var second = await host.ExternalRefService.AddAsync(id,
            new CreateExternalRefRequest("legalsynq", "tenant-2", null, false), default);

        var promoted = await host.ExternalRefService.MakePrimaryAsync(id, second.Id, default);
        Assert.True(promoted.IsPrimary);

        var refreshed = await host.Db.BillingAccountExternalRefs.AsNoTracking()
            .SingleAsync(r => r.Id == first.Id);
        Assert.False(refreshed.IsPrimary);
    }

    [Fact]
    public async Task List_returns_all_for_account()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);
        await host.ExternalRefService.AddAsync(id,
            new CreateExternalRefRequest("legalsynq", "t1", null, true), default);
        await host.ExternalRefService.AddAsync(id,
            new CreateExternalRefRequest("legalsynq", "t2", null, false), default);

        var list = await host.ExternalRefService.ListAsync(id, default);
        Assert.Equal(2, list.Count);
    }
}
