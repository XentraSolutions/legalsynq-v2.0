using System.Net;
using System.Net.Http.Json;
using Billing.Api.Contracts;
using Billing.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// TB-DATA-02 — round-trip API tests for
/// /api/tenant-billing/entitlements/* and the per-profile entitlement
/// route.
/// </summary>
public class TenantBillingEntitlementsApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;
    public TenantBillingEntitlementsApiTests(BillingWebApplicationFactory f) => _factory = f;

    private static async Task<TenantBillingProfileResponse> CreateProfileAsync(
        HttpClient client, Guid billingAccount)
    {
        var resp = await client.PostAsJsonAsync("/api/tenant-billing/profiles",
            new CreateTenantBillingProfileRequest
            {
                BillingAccountId = billingAccount,
                Mode = TenantBillingMode.InternalOnly,
            });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!;
    }

    private static async Task<TenantBillingProfileResponse> ActivateAsync(
        HttpClient client, Guid profileId)
    {
        var resp = await client.PostAsync($"/api/tenant-billing/profiles/{profileId}/activate", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!;
    }

    private static ApplyEntitlementSnapshotRequestDto Req(
        Guid account,
        string status = TenantBillingEntitlementStatus.Enabled,
        string rec    = TenantBillingAccessRecommendation.Allow) => new()
    {
        BillingAccountId     = account,
        SourceSystem         = "commerce",
        EntitlementStatus    = status,
        AccessRecommendation = rec,
        SourceSubscriptionId = "sub-123",
        SourcePlanKey        = "pro-monthly",
    };

    [Fact]
    public async Task Apply_returns_200_and_creates_snapshot_for_active_profile()
    {
        var t = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, a);
        await ActivateAsync(c, prof.Id);

        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", Req(a));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var snap = (await resp.Content.ReadFromJsonAsync<TenantBillingEntitlementSnapshotResponse>())!;
        snap.TenantId.Should().Be(t);
        snap.EntitlementStatus.Should().Be(TenantBillingEntitlementStatus.Enabled);
    }

    [Fact]
    public async Task Apply_updates_in_place_on_second_call()
    {
        var t = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, a);
        await ActivateAsync(c, prof.Id);

        var first = (await (await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", Req(a)))
            .Content.ReadFromJsonAsync<TenantBillingEntitlementSnapshotResponse>())!;
        var second = (await (await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply",
            Req(a, TenantBillingEntitlementStatus.Suspended, TenantBillingAccessRecommendation.Block)))
            .Content.ReadFromJsonAsync<TenantBillingEntitlementSnapshotResponse>())!;
        second.Id.Should().Be(first.Id);
        second.EntitlementStatus.Should().Be(TenantBillingEntitlementStatus.Suspended);
    }

    [Fact]
    public async Task Apply_returns_404_when_no_profile_exists()
    {
        var c = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", Req(Guid.CreateVersion7()));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Apply_returns_409_when_profile_is_closed()
    {
        var t = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, a);
        var close = await c.PostAsync($"/api/tenant-billing/profiles/{prof.Id}/close", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);

        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", Req(a));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Apply_returns_409_when_billing_account_does_not_match_active_profile()
    {
        var t = Guid.CreateVersion7();
        var realAccount = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, realAccount);
        await ActivateAsync(c, prof.Id);

        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply",
            Req(Guid.CreateVersion7()));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Apply_returns_400_for_bad_enum()
    {
        var t = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, a);
        await ActivateAsync(c, prof.Id);

        var bad = Req(a);
        bad.EntitlementStatus = "Bogus";
        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", bad);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Apply_returns_400_for_invalid_raw_json()
    {
        var t = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, a);
        await ActivateAsync(c, prof.Id);

        var bad = Req(a);
        bad.RawSnapshotJson = "{nope";
        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", bad);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCurrent_returns_snapshot_after_apply()
    {
        var t = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, a);
        await ActivateAsync(c, prof.Id);
        await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", Req(a));

        var resp = await c.GetAsync("/api/tenant-billing/entitlements/current");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCurrent_returns_404_when_no_snapshot_yet()
    {
        var c = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await c.GetAsync("/api/tenant-billing/entitlements/current");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAccess_reports_enabled_for_Active_Enabled_Allow()
    {
        var t = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var c = _factory.CreateClientForTenant(t);
        var prof = await CreateProfileAsync(c, a);
        await ActivateAsync(c, prof.Id);
        await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", Req(a));

        var resp = await c.GetAsync("/api/tenant-billing/entitlements/access");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var d = (await resp.Content.ReadFromJsonAsync<TenantBillingAccessDecisionResponse>())!;
        d.IsEnabled.Should().BeTrue();
        d.WriteAccessAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccess_reports_NotEnabled_when_no_profile()
    {
        var c = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await c.GetAsync("/api/tenant-billing/entitlements/access");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var d = (await resp.Content.ReadFromJsonAsync<TenantBillingAccessDecisionResponse>())!;
        d.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetByProfile_returns_snapshot_for_owner_404_for_stranger()
    {
        var ownerT = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        var owner = _factory.CreateClientForTenant(ownerT);
        var prof = await CreateProfileAsync(owner, a);
        await ActivateAsync(owner, prof.Id);
        await owner.PostAsJsonAsync("/api/tenant-billing/entitlements/apply", Req(a));

        var ownerResp = await owner.GetAsync($"/api/tenant-billing/profiles/{prof.Id}/entitlement");
        ownerResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var stranger = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var strangerResp = await stranger.GetAsync($"/api/tenant-billing/profiles/{prof.Id}/entitlement");
        strangerResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Apply_without_tenant_header_returns_400()
    {
        var c = _factory.CreateClient(); // no tenant header
        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply",
            Req(Guid.CreateVersion7()));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Apply_without_internal_token_returns_401()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Remove(
            Billing.Api.Security.RequireInternalTokenMiddleware.HeaderName);
        c.DefaultRequestHeaders.Add(
            Billing.Api.Tenancy.TenantResolutionMiddleware.HeaderName, Guid.CreateVersion7().ToString());
        var resp = await c.PostAsJsonAsync("/api/tenant-billing/entitlements/apply",
            Req(Guid.CreateVersion7()));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
