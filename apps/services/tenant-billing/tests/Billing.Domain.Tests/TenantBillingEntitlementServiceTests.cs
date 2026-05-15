using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// TB-DATA-02 — domain tests for <see cref="TenantBillingEntitlementService"/>.
/// Covers apply / update / mismatch / closed-profile / invalid JSON / invalid
/// enum / tenant isolation.
/// </summary>
public class TenantBillingEntitlementServiceTests
{
    private static (TenantBillingProfileService profileSvc,
                    TenantBillingEntitlementService entitlementSvc,
                    InMemoryTenantBillingProfileRepository profileRepo,
                    InMemoryTenantBillingEntitlementSnapshotRepository snapRepo) Build()
    {
        var profileRepo = new InMemoryTenantBillingProfileRepository();
        var snapRepo    = new InMemoryTenantBillingEntitlementSnapshotRepository();
        var clock       = TimeProvider.System;
        var pSvc        = new TenantBillingProfileService(profileRepo, clock);
        var eSvc        = new TenantBillingEntitlementService(profileRepo, snapRepo, clock);
        return (pSvc, eSvc, profileRepo, snapRepo);
    }

    private static async Task<TenantBillingProfile> SeedActiveProfileAsync(
        TenantBillingProfileService p, Guid tenantId, Guid billingAccountId)
    {
        var draft = await p.CreateAsync(tenantId, billingAccountId, null, null,
            TenantBillingMode.InternalOnly, null);
        return await p.ActivateAsync(tenantId, draft.Id);
    }

    private static ApplyEntitlementSnapshotRequest Req(
        Guid billingAccountId,
        string status = TenantBillingEntitlementStatus.Enabled,
        string rec    = TenantBillingAccessRecommendation.Allow,
        string? json  = null)
        => new(billingAccountId, "commerce", status, rec,
               SourceSnapshotId: "snap-1", SourceSubscriptionId: "sub-1",
               SourcePlanKey: "pro-monthly", SourceProductKey: "tenant-billing",
               Reason: null, EffectiveFromUtc: null, EffectiveToUtc: null,
               RawSnapshotJson: json);

    [Fact]
    public async Task Apply_creates_first_snapshot_for_active_profile()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        await SeedActiveProfileAsync(p, tenant, account);

        var snap = await e.ApplySnapshotAsync(tenant, Req(account));

        snap.TenantId.Should().Be(tenant);
        snap.BillingAccountId.Should().Be(account);
        snap.EntitlementStatus.Should().Be(TenantBillingEntitlementStatus.Enabled);
        snap.AccessRecommendation.Should().Be(TenantBillingAccessRecommendation.Allow);
        snap.SourceSubscriptionId.Should().Be("sub-1");
    }

    [Fact]
    public async Task Apply_updates_existing_snapshot_in_place()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        await SeedActiveProfileAsync(p, tenant, account);

        var first  = await e.ApplySnapshotAsync(tenant, Req(account));
        var second = await e.ApplySnapshotAsync(tenant,
            Req(account, TenantBillingEntitlementStatus.Suspended,
                         TenantBillingAccessRecommendation.Block));

        second.Id.Should().Be(first.Id, "in-place update preserves the row id");
        second.EntitlementStatus.Should().Be(TenantBillingEntitlementStatus.Suspended);
        second.AccessRecommendation.Should().Be(TenantBillingAccessRecommendation.Block);
    }

    [Fact]
    public async Task Apply_throws_NotFound_when_no_open_profile()
    {
        var (_, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();

        await Assert.ThrowsAsync<TenantBillingProfileNotFoundException>(
            () => e.ApplySnapshotAsync(tenant, Req(account)));
    }

    [Fact]
    public async Task Apply_throws_Mismatch_when_BillingAccount_does_not_match_active_profile()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var realAccount = Guid.NewGuid();
        var bogusAccount = Guid.NewGuid();
        await SeedActiveProfileAsync(p, tenant, realAccount);

        await Assert.ThrowsAsync<TenantBillingEntitlementProfileMismatchException>(
            () => e.ApplySnapshotAsync(tenant, Req(bogusAccount)));
    }

    [Fact]
    public async Task Apply_throws_ClosedProfile_when_profile_is_closed()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        var draft = await p.CreateAsync(tenant, account, null, null,
            TenantBillingMode.InternalOnly, null);
        await p.CloseAsync(tenant, draft.Id);

        await Assert.ThrowsAsync<TenantBillingEntitlementClosedProfileException>(
            () => e.ApplySnapshotAsync(tenant, Req(account)));
    }

    [Fact]
    public async Task Apply_throws_for_invalid_status_enum()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        await SeedActiveProfileAsync(p, tenant, account);

        await Assert.ThrowsAsync<ArgumentException>(
            () => e.ApplySnapshotAsync(tenant, Req(account, status: "Bogus")));
    }

    [Fact]
    public async Task Apply_throws_InvalidJson_for_malformed_raw_snapshot()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        await SeedActiveProfileAsync(p, tenant, account);

        await Assert.ThrowsAsync<TenantBillingEntitlementInvalidJsonException>(
            () => e.ApplySnapshotAsync(tenant, Req(account, json: "{not valid")));
    }

    [Fact]
    public async Task Apply_accepts_well_formed_raw_snapshot_json()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        await SeedActiveProfileAsync(p, tenant, account);

        var snap = await e.ApplySnapshotAsync(tenant,
            Req(account, json: "{\"hello\":\"world\",\"n\":42}"));
        snap.RawSnapshotJson.Should().Contain("hello");
    }

    [Fact]
    public async Task GetCurrent_returns_null_when_no_active_profile()
    {
        var (_, e, _, _) = Build();
        var snap = await e.GetCurrentSnapshotAsync(Guid.NewGuid());
        snap.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrent_returns_snapshot_for_active_profile()
    {
        var (p, e, _, _) = Build();
        var tenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        await SeedActiveProfileAsync(p, tenant, account);
        await e.ApplySnapshotAsync(tenant, Req(account));

        var snap = await e.GetCurrentSnapshotAsync(tenant);
        snap.Should().NotBeNull();
        snap!.BillingAccountId.Should().Be(account);
    }

    [Fact]
    public async Task GetByProfileId_is_tenant_scoped()
    {
        var (p, e, _, _) = Build();
        var ownerTenant = Guid.NewGuid();
        var strangerTenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        var profile = await SeedActiveProfileAsync(p, ownerTenant, account);
        await e.ApplySnapshotAsync(ownerTenant, Req(account));

        (await e.GetByProfileIdAsync(strangerTenant, profile.Id)).Should().BeNull();
        (await e.GetByProfileIdAsync(ownerTenant,    profile.Id)).Should().NotBeNull();
    }
}
