using TenantBilling.Domain.Entities;
using TenantBilling.Domain.StatementTemplates;
using TenantBilling.Domain.Tests.Fakes;
using Xunit;

namespace TenantBilling.Domain.Tests;

/// <summary>
/// STAT-B02 — Service-layer tests for the tenant-scoped statement
/// template catalogue. Covers create / update / lifecycle / default
/// promotion / selection.
/// </summary>
public class StatementTemplateServiceTests
{
    private static (StatementTemplateService svc, InMemoryStatementTemplateRepository repo)
        CreateService()
    {
        var repo = new InMemoryStatementTemplateRepository();
        var uow = new InMemoryUnitOfWork();
        return (new StatementTemplateService(repo, uow), repo);
    }

    private static NewStatementTemplate Sample(
        string? name = "Brand A",
        string? status = null,
        bool? isDefault = null) => new(
        Name: name!,
        Description: "desc",
        Status: status,
        IsDefault: isDefault,
        AccentColor: "#1f4fff",
        StatementNumberPrefix: "stmt-a");

    [Fact]
    public async Task CreateAsync_DefaultsToDraft_AndNotDefault()
    {
        var (svc, _) = CreateService();
        var t = await svc.CreateAsync(Guid.CreateVersion7(), Sample());
        Assert.Equal(StatementTemplateStatus.Draft, t.Status);
        Assert.False(t.IsDefault);
        Assert.Equal("#1F4FFF", t.AccentColor); // normalized to upper
        Assert.Equal("STMT-A", t.StatementNumberPrefix); // normalized to upper
    }

    [Fact]
    public async Task CreateAsync_FirstActiveIsAutoDefaulted()
    {
        var (svc, _) = CreateService();
        var t = await svc.CreateAsync(Guid.CreateVersion7(), Sample(status: StatementTemplateStatus.Active));
        Assert.True(t.IsDefault);
    }

    [Fact]
    public async Task CreateAsync_SecondActiveIsNotAutoDefaulted()
    {
        var (svc, _) = CreateService();
        var tenant = Guid.CreateVersion7();
        var first = await svc.CreateAsync(tenant, Sample(name: "A", status: StatementTemplateStatus.Active));
        var second = await svc.CreateAsync(tenant, Sample(name: "B", status: StatementTemplateStatus.Active));
        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);
    }

    [Fact]
    public async Task CreateAsync_ExplicitDefault_RequiresActive()
    {
        var (svc, _) = CreateService();
        await Assert.ThrowsAsync<InvalidStatementTemplateStatusTransitionException>(() =>
            svc.CreateAsync(Guid.CreateVersion7(),
                Sample(status: StatementTemplateStatus.Draft, isDefault: true)));
    }

    [Fact]
    public async Task CreateAsync_RejectsBadAccentColor()
    {
        var (svc, _) = CreateService();
        var bad = Sample() with { AccentColor = "not-a-color" };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.CreateVersion7(), bad));
    }

    [Fact]
    public async Task CreateAsync_TenantsAreIsolated()
    {
        var (svc, repo) = CreateService();
        var t1 = Guid.CreateVersion7(); var t2 = Guid.CreateVersion7();
        await svc.CreateAsync(t1, Sample(name: "A", status: StatementTemplateStatus.Active));
        await svc.CreateAsync(t2, Sample(name: "B", status: StatementTemplateStatus.Active));
        var l1 = await repo.ListInScopeAsync(t1); var l2 = await repo.ListInScopeAsync(t2);
        Assert.Single(l1); Assert.Single(l2); Assert.NotEqual(l1[0].Id, l2[0].Id);
    }

    [Fact]
    public async Task MakeDefaultAsync_PromotesAndDemotesPeers()
    {
        var (svc, _) = CreateService();
        var tenant = Guid.CreateVersion7();
        var a = await svc.CreateAsync(tenant, Sample(name: "A", status: StatementTemplateStatus.Active));
        var b = await svc.CreateAsync(tenant, Sample(name: "B", status: StatementTemplateStatus.Active));
        Assert.True(a.IsDefault);
        Assert.False(b.IsDefault);

        var promoted = await svc.MakeDefaultAsync(tenant, b.Id);
        Assert.NotNull(promoted);
        Assert.True(promoted!.IsDefault);

        var listed = await svc.ListAsync(tenant);
        Assert.Single(listed.Where(t => t.IsDefault));
        Assert.Equal(b.Id, listed.First(t => t.IsDefault).Id);
    }

    [Fact]
    public async Task MakeDefaultAsync_RejectsRetired()
    {
        var (svc, _) = CreateService();
        var tenant = Guid.CreateVersion7();
        var t = await svc.CreateAsync(tenant, Sample(status: StatementTemplateStatus.Active));
        await svc.RetireAsync(tenant, t.Id);
        await Assert.ThrowsAsync<RetiredStatementTemplateCannotBeDefaultException>(() =>
            svc.MakeDefaultAsync(tenant, t.Id));
    }

    [Fact]
    public async Task RetireAsync_ClearsDefaultFlag()
    {
        var (svc, _) = CreateService();
        var tenant = Guid.CreateVersion7();
        var t = await svc.CreateAsync(tenant, Sample(status: StatementTemplateStatus.Active));
        Assert.True(t.IsDefault);
        var retired = await svc.RetireAsync(tenant, t.Id);
        Assert.NotNull(retired);
        Assert.Equal(StatementTemplateStatus.Retired, retired!.Status);
        Assert.False(retired.IsDefault);
    }

    [Fact]
    public async Task UpdateAsync_RejectsRetired()
    {
        var (svc, _) = CreateService();
        var tenant = Guid.CreateVersion7();
        var t = await svc.CreateAsync(tenant, Sample(status: StatementTemplateStatus.Active));
        await svc.RetireAsync(tenant, t.Id);
        await Assert.ThrowsAsync<InvalidStatementTemplateStatusTransitionException>(() =>
            svc.UpdateAsync(tenant, t.Id, new StatementTemplateUpdate(Name: "X")));
    }

    [Fact]
    public async Task SelectForStatementAsync_FallsBackToDefaultThenNull()
    {
        var (svc, _) = CreateService();
        var tenant = Guid.CreateVersion7();

        // No templates -> null.
        Assert.Null(await svc.SelectForStatementAsync(tenant, null));

        // Add active default -> picked.
        var def = await svc.CreateAsync(tenant, Sample(status: StatementTemplateStatus.Active));
        var picked = await svc.SelectForStatementAsync(tenant, null);
        Assert.NotNull(picked);
        Assert.Equal(def.Id, picked!.Id);
    }

    [Fact]
    public async Task SelectForStatementAsync_ExplicitDraft_Throws()
    {
        var (svc, _) = CreateService();
        var tenant = Guid.CreateVersion7();
        var t = await svc.CreateAsync(tenant, Sample()); // Draft
        await Assert.ThrowsAsync<StatementTemplateNotSelectableException>(() =>
            svc.SelectForStatementAsync(tenant, t.Id));
    }

    [Fact]
    public async Task SelectForStatementAsync_UnknownExplicit_Throws()
    {
        var (svc, _) = CreateService();
        await Assert.ThrowsAsync<StatementTemplateNotFoundInScopeException>(() =>
            svc.SelectForStatementAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));
    }

    [Fact]
    public async Task SelectForStatementAsync_CrossTenantId_Throws()
    {
        var (svc, _) = CreateService();
        var t1 = Guid.CreateVersion7();
        var template = await svc.CreateAsync(t1, Sample(status: StatementTemplateStatus.Active));

        var t2 = Guid.CreateVersion7();
        await Assert.ThrowsAsync<StatementTemplateNotFoundInScopeException>(() =>
            svc.SelectForStatementAsync(t2, template.Id));
    }
}

/// <summary>Tiny test helper for record reuse with overrides.</summary>
internal static class NewStatementTemplateExtensions
{
    public static NewStatementTemplate AsNew(this NewStatementTemplate n) => n;
}
