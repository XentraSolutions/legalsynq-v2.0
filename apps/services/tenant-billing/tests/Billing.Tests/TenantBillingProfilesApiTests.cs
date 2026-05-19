using System.Net;
using System.Net.Http.Json;
using Billing.Api.Contracts;
using Billing.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// TB-DATA-01 — round-trip API tests for /api/tenant-billing/profiles.
/// Exercises create / list / get / by-billing-account / activate / suspend
/// / close, plus cross-tenant isolation and conflict mapping.
/// </summary>
public class TenantBillingProfilesApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;
    public TenantBillingProfilesApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private static async Task<TenantBillingProfileResponse> CreateAsync(
        HttpClient client, Guid? billingAccount = null, string? mode = null)
    {
        var resp = await client.PostAsJsonAsync("/api/tenant-billing/profiles",
            new CreateTenantBillingProfileRequest
            {
                BillingAccountId = billingAccount ?? Guid.CreateVersion7(),
                Mode = mode ?? TenantBillingMode.InternalOnly,
            });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!;
    }

    [Fact]
    public async Task Create_returns_201_with_draft_profile()
    {
        var tenant = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenant);
        var account = Guid.CreateVersion7();

        var dto = await CreateAsync(client, account);
        dto.TenantId.Should().Be(tenant);
        dto.BillingAccountId.Should().Be(account);
        dto.Status.Should().Be(TenantBillingProfileStatus.Draft);
    }

    [Fact]
    public async Task Lifecycle_round_trip_draft_active_suspended_active_closed()
    {
        var tenant = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenant);
        var dto = await CreateAsync(client);

        var act = await client.PostAsync($"/api/tenant-billing/profiles/{dto.Id}/activate", null);
        act.StatusCode.Should().Be(HttpStatusCode.OK);
        (await act.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!.Status
            .Should().Be(TenantBillingProfileStatus.Active);

        var sus = await client.PostAsync($"/api/tenant-billing/profiles/{dto.Id}/suspend", null);
        sus.StatusCode.Should().Be(HttpStatusCode.OK);
        (await sus.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!.Status
            .Should().Be(TenantBillingProfileStatus.Suspended);

        var reactivate = await client.PostAsync($"/api/tenant-billing/profiles/{dto.Id}/activate", null);
        reactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        var close = await client.PostAsync($"/api/tenant-billing/profiles/{dto.Id}/close", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);
        (await close.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!.Status
            .Should().Be(TenantBillingProfileStatus.Closed);

        // Re-activating a Closed profile is a 409.
        var reanimate = await client.PostAsync($"/api/tenant-billing/profiles/{dto.Id}/activate", null);
        reanimate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_returns_409_when_tenant_already_has_open_profile()
    {
        var tenant = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenant);
        await CreateAsync(client);

        var second = await client.PostAsJsonAsync("/api/tenant-billing/profiles",
            new CreateTenantBillingProfileRequest
            {
                BillingAccountId = Guid.CreateVersion7(),
                Mode = TenantBillingMode.InternalOnly,
            });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_returns_409_when_billing_account_already_claimed_by_other_tenant()
    {
        var account = Guid.CreateVersion7();
        var clientA = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var clientB = _factory.CreateClientForTenant(Guid.CreateVersion7());

        await CreateAsync(clientA, account);

        var dup = await clientB.PostAsJsonAsync("/api/tenant-billing/profiles",
            new CreateTenantBillingProfileRequest
            {
                BillingAccountId = account,
                Mode = TenantBillingMode.InternalOnly,
            });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetById_is_tenant_scoped_returns_404_for_stranger()
    {
        var owner = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var dto = await CreateAsync(owner);

        var stranger = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await stranger.GetAsync($"/api/tenant-billing/profiles/{dto.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByBillingAccount_returns_active_profile()
    {
        var tenant = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenant);
        var account = Guid.CreateVersion7();
        var dto = await CreateAsync(client, account);
        await client.PostAsync($"/api/tenant-billing/profiles/{dto.Id}/activate", null);

        var resp = await client.GetAsync($"/api/tenant-billing/profiles/by-billing-account/{account}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<TenantBillingProfileResponse>())!.Id.Should().Be(dto.Id);
    }

    [Fact]
    public async Task GetByBillingAccount_returns_404_when_unknown()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await client.GetAsync($"/api/tenant-billing/profiles/by-billing-account/{Guid.CreateVersion7()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Suspend_on_draft_returns_409()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var dto = await CreateAsync(client);

        var resp = await client.PostAsync($"/api/tenant-billing/profiles/{dto.Id}/suspend", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Activate_on_unknown_id_returns_404()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var resp = await client.PostAsync($"/api/tenant-billing/profiles/{Guid.CreateVersion7()}/activate", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_returns_only_current_tenant_profiles()
    {
        var t1 = _factory.CreateClientForTenant(Guid.CreateVersion7());
        var t2 = _factory.CreateClientForTenant(Guid.CreateVersion7());
        await CreateAsync(t1);
        await CreateAsync(t2);

        var resp = await t1.GetAsync("/api/tenant-billing/profiles");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await resp.Content.ReadFromJsonAsync<TenantBillingProfileListResponse>())!;
        page.TotalCount.Should().Be(1);
    }
}
