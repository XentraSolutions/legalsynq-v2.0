using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace Billing.Domain.Tests;

public class TenantBillingProfileServiceTests
{
    private static (TenantBillingProfileService svc, InMemoryTenantBillingProfileRepository repo, TenantBillingAccountResolver resolver) Build()
    {
        var repo = new InMemoryTenantBillingProfileRepository();
        var svc  = new TenantBillingProfileService(repo);
        var resolver = new TenantBillingAccountResolver(repo);
        return (svc, repo, resolver);
    }

    [Fact]
    public async Task Create_persists_draft_with_normalized_optional_fields()
    {
        var (svc, _, _) = Build();
        var tenant = Guid.CreateVersion7();
        var account = Guid.CreateVersion7();

        var p = await svc.CreateAsync(tenant, account,
            hostPlatformKey: "  monk  ",
            externalTenantId: "  tenant-slug  ",
            mode: TenantBillingMode.InternalOnly,
            notes: "  ");

        p.TenantId.Should().Be(tenant);
        p.BillingAccountId.Should().Be(account);
        p.Status.Should().Be(TenantBillingProfileStatus.Draft);
        p.HostPlatformKey.Should().Be("monk");
        p.ExternalTenantId.Should().Be("tenant-slug");
        p.Notes.Should().BeNull();
    }

    [Fact]
    public async Task Create_rejects_empty_tenant_or_account()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.Empty, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.CreateVersion7(), Guid.Empty, null, null, TenantBillingMode.InternalOnly, null));
    }

    [Fact]
    public async Task Create_rejects_unknown_mode()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, "Bogus", null));
    }

    [Fact]
    public async Task Create_conflicts_when_tenant_already_has_open_profile()
    {
        var (svc, _, _) = Build();
        var tenant = Guid.CreateVersion7();
        await svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);

        await Assert.ThrowsAsync<TenantBillingProfileConflictException>(() =>
            svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null));
    }

    [Fact]
    public async Task Create_conflicts_when_billing_account_already_claimed_cross_tenant()
    {
        var (svc, _, _) = Build();
        var account = Guid.CreateVersion7();
        await svc.CreateAsync(Guid.CreateVersion7(), account, null, null, TenantBillingMode.InternalOnly, null);

        await Assert.ThrowsAsync<TenantBillingProfileConflictException>(() =>
            svc.CreateAsync(Guid.CreateVersion7(), account, null, null, TenantBillingMode.InternalOnly, null));
    }

    [Fact]
    public async Task Activate_promotes_draft_to_active_and_stamps_activated_at()
    {
        var (svc, _, _) = Build();
        var tenant = Guid.CreateVersion7();
        var p = await svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);

        var activated = await svc.ActivateAsync(tenant, p.Id);

        activated.Status.Should().Be(TenantBillingProfileStatus.Active);
        activated.ActivatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Suspend_then_reactivate_keeps_original_activated_at()
    {
        var (svc, _, _) = Build();
        var tenant = Guid.CreateVersion7();
        var p = await svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);

        var activated = await svc.ActivateAsync(tenant, p.Id);
        var firstActivation = activated.ActivatedAtUtc!.Value;

        await svc.SuspendAsync(tenant, p.Id);
        var reactivated = await svc.ActivateAsync(tenant, p.Id);

        reactivated.Status.Should().Be(TenantBillingProfileStatus.Active);
        reactivated.ActivatedAtUtc.Should().Be(firstActivation);
    }

    [Fact]
    public async Task Suspend_on_draft_is_rejected()
    {
        var (svc, _, _) = Build();
        var tenant = Guid.CreateVersion7();
        var p = await svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);

        await Assert.ThrowsAsync<InvalidTenantBillingProfileTransitionException>(() =>
            svc.SuspendAsync(tenant, p.Id));
    }

    [Fact]
    public async Task Close_is_terminal_and_blocks_subsequent_transitions()
    {
        var (svc, _, _) = Build();
        var tenant = Guid.CreateVersion7();
        var p = await svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);
        await svc.ActivateAsync(tenant, p.Id);
        await svc.CloseAsync(tenant, p.Id);

        await Assert.ThrowsAsync<InvalidTenantBillingProfileTransitionException>(() =>
            svc.ActivateAsync(tenant, p.Id));
        await Assert.ThrowsAsync<InvalidTenantBillingProfileTransitionException>(() =>
            svc.SuspendAsync(tenant, p.Id));
    }

    [Fact]
    public async Task Close_allows_creating_new_profile_for_same_tenant()
    {
        var (svc, _, _) = Build();
        var tenant = Guid.CreateVersion7();
        var first = await svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);
        await svc.CloseAsync(tenant, first.Id);

        var second = await svc.CreateAsync(tenant, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);
        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task Get_returns_null_for_other_tenant()
    {
        var (svc, _, _) = Build();
        var owner = Guid.CreateVersion7();
        var p = await svc.CreateAsync(owner, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);

        var stranger = Guid.CreateVersion7();
        (await svc.GetAsync(stranger, p.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Lifecycle_on_other_tenant_id_is_404()
    {
        var (svc, _, _) = Build();
        var owner = Guid.CreateVersion7();
        var p = await svc.CreateAsync(owner, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);

        await Assert.ThrowsAsync<TenantBillingProfileNotFoundException>(() =>
            svc.ActivateAsync(Guid.CreateVersion7(), p.Id));
    }

    [Fact]
    public async Task Resolver_returns_billing_account_only_for_active_profile()
    {
        var (svc, _, resolver) = Build();
        var tenant = Guid.CreateVersion7();
        var account = Guid.CreateVersion7();
        var p = await svc.CreateAsync(tenant, account, null, null, TenantBillingMode.InternalOnly, null);

        // Draft → null
        (await resolver.GetBillingAccountIdAsync(tenant)).Should().BeNull();

        await svc.ActivateAsync(tenant, p.Id);
        (await resolver.GetBillingAccountIdAsync(tenant)).Should().Be(account);

        await svc.SuspendAsync(tenant, p.Id);
        (await resolver.GetBillingAccountIdAsync(tenant)).Should().BeNull();

        await svc.ActivateAsync(tenant, p.Id);
        await svc.CloseAsync(tenant, p.Id);
        (await resolver.GetBillingAccountIdAsync(tenant)).Should().BeNull();
    }

    [Fact]
    public async Task Resolver_returns_null_for_unknown_or_empty_tenant()
    {
        var (_, _, resolver) = Build();
        (await resolver.GetBillingAccountIdAsync(Guid.CreateVersion7())).Should().BeNull();
        (await resolver.GetBillingAccountIdAsync(Guid.Empty)).Should().BeNull();
    }

    [Fact]
    public async Task GetByBillingAccount_is_tenant_scoped()
    {
        var (svc, _, _) = Build();
        var account = Guid.CreateVersion7();
        var ownerA = Guid.CreateVersion7();
        await svc.CreateAsync(ownerA, account, null, null, TenantBillingMode.InternalOnly, null);

        var ownerB = Guid.CreateVersion7();
        (await svc.GetByBillingAccountAsync(ownerB, account)).Should().BeNull();
        (await svc.GetByBillingAccountAsync(ownerA, account)).Should().NotBeNull();
    }

    [Fact]
    public async Task List_pages_only_within_tenant()
    {
        var (svc, _, _) = Build();
        var t1 = Guid.CreateVersion7();
        var t2 = Guid.CreateVersion7();
        await svc.CreateAsync(t1, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);
        await svc.CreateAsync(t2, Guid.CreateVersion7(), null, null, TenantBillingMode.InternalOnly, null);

        var page = await svc.ListAsync(t1, 1, 25);
        page.TotalCount.Should().Be(1);
        page.Items.Single().TenantId.Should().Be(t1);
    }
}
