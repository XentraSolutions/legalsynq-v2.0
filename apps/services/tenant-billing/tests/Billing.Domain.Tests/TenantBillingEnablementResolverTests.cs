using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// TB-DATA-02 — full decision matrix for
/// <see cref="TenantBillingEnablementResolver"/>. Mirrors §9 of the report.
/// </summary>
public class TenantBillingEnablementResolverTests
{
    private static (TenantBillingProfileService p,
                    TenantBillingEntitlementService e,
                    TenantBillingEnablementResolver r) Build()
    {
        var profiles = new InMemoryTenantBillingProfileRepository();
        var snaps    = new InMemoryTenantBillingEntitlementSnapshotRepository();
        var clock    = TimeProvider.System;
        var pSvc     = new TenantBillingProfileService(profiles, clock);
        var eSvc     = new TenantBillingEntitlementService(profiles, snaps, clock);
        return (pSvc, eSvc, new TenantBillingEnablementResolver(eSvc));
    }

    private static async Task<Guid> SeedActiveAsync(
        TenantBillingProfileService p, Guid tenant, Guid account)
    {
        var draft = await p.CreateAsync(tenant, account, null, null,
            TenantBillingMode.InternalOnly, null);
        await p.ActivateAsync(tenant, draft.Id);
        return draft.Id;
    }

    private static ApplyEntitlementSnapshotRequest Req(Guid account, string status, string rec)
        => new(account, "commerce", status, rec,
               null, null, null, null, null, null, null, null);

    [Fact]
    public async Task Missing_profile_yields_NotEnabled_Unknown()
    {
        var (_, _, r) = Build();
        var d = await r.GetTenantBillingAccessAsync(Guid.CreateVersion7());
        d.IsEnabled.Should().BeFalse();
        d.EntitlementStatus.Should().Be(TenantBillingEntitlementStatus.Unknown);
        d.AccessRecommendation.Should().Be(TenantBillingAccessRecommendation.Unknown);
    }

    [Fact]
    public async Task Active_profile_without_snapshot_is_NotEnabled_Unknown()
    {
        var (p, _, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        var d = await r.GetTenantBillingAccessAsync(t);
        d.IsEnabled.Should().BeFalse();
        d.EntitlementStatus.Should().Be(TenantBillingEntitlementStatus.Unknown);
    }

    [Fact]
    public async Task Active_profile_Enabled_Allow_is_Enabled()
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Allow));

        (await r.IsTenantBillingEnabledAsync(t)).Should().BeTrue();
        var d = await r.GetTenantBillingAccessAsync(t);
        d.IsEnabled.Should().BeTrue();
        d.WriteAccessAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(TenantBillingEntitlementStatus.Disabled,  TenantBillingAccessRecommendation.Block)]
    [InlineData(TenantBillingEntitlementStatus.Suspended, TenantBillingAccessRecommendation.Block)]
    [InlineData(TenantBillingEntitlementStatus.Expired,   TenantBillingAccessRecommendation.Block)]
    [InlineData(TenantBillingEntitlementStatus.Enabled,   TenantBillingAccessRecommendation.Block)]
    [InlineData(TenantBillingEntitlementStatus.Enabled,   TenantBillingAccessRecommendation.ReadOnly)]
    [InlineData(TenantBillingEntitlementStatus.Enabled,   TenantBillingAccessRecommendation.GraceLimited)]
    [InlineData(TenantBillingEntitlementStatus.Enabled,   TenantBillingAccessRecommendation.Unknown)]
    [InlineData(TenantBillingEntitlementStatus.Unknown,   TenantBillingAccessRecommendation.Allow)]
    public async Task Active_profile_with_non_allow_combinations_are_NotEnabled(string status, string rec)
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a, status, rec));

        (await r.IsTenantBillingEnabledAsync(t)).Should().BeFalse();
    }

    [Fact]
    public async Task Suspended_profile_is_never_enabled_even_with_allow_snapshot()
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        var pid = await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Allow));
        await p.SuspendAsync(t, pid);

        (await r.IsTenantBillingEnabledAsync(t)).Should().BeFalse();
    }

    [Fact]
    public async Task Closed_profile_is_never_enabled()
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        var pid = await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Allow));
        await p.CloseAsync(t, pid);

        (await r.IsTenantBillingEnabledAsync(t)).Should().BeFalse();
    }
}
