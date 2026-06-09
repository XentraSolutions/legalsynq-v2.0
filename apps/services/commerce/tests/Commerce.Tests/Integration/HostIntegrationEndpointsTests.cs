using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Integration;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration;

/// <summary>
/// Surface-level checks for the COM-B08 host-integration controller.
/// The DB is the shared in-memory fallback (its name is fixed in DI),
/// so other tests in the same run may seed records into it. We
/// therefore avoid asserting on absolute totals and stick to
/// shape/status-code assertions.
/// </summary>
public class HostIntegrationEndpointsTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;

    public HostIntegrationEndpointsTests(CommerceWebApplicationFactory factory)
        => _factory = factory;

    [Fact]
    public async Task Health_endpoint_returns_ok_with_adapter_metadata()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/integration/contracts/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<IntegrationContractsHealthResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("ok");
        body.IdentityContextAccessor.Should().Contain("LocalHostIdentityContextAccessor");
        body.TenantResolver.Should().Contain("NoopHostTenantResolver");
        body.ProvisioningHookPublisher.Should().Be("noop");
    }

    [Fact]
    public async Task Snapshot_by_unknown_billing_account_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            $"/api/commerce/integration/billing-accounts/{Guid.CreateVersion7()}/entitlement-snapshot");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Snapshot_by_unknown_host_tenant_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            $"/api/commerce/integration/host-tenants/never-registered/abc-{Guid.CreateVersion7():N}/entitlement-snapshot");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recommendation_for_unknown_billing_account_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            $"/api/commerce/integration/billing-accounts/{Guid.CreateVersion7()}/access-recommendation");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Snapshot_endpoint_round_trips_for_seeded_account()
    {
        var client = _factory.CreateClient();

        // Create account + external ref through the public Billing API
        // so we exercise the same path a host integrator would.
        var hostKey = $"host-{Guid.CreateVersion7():N}".Substring(0, 16);
        var tenantId = $"tnt-{Guid.CreateVersion7():N}".Substring(0, 16);

        var createAcct = await client.PostAsJsonAsync("/api/commerce/billing-accounts", new
        {
            displayName = "Acme Integration",
            legalName = (string?)null,
            defaultCurrency = "USD"
        });
        createAcct.EnsureSuccessStatusCode();
        var acctBody = await createAcct.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var billingAccountId = Guid.Parse(acctBody!["id"].ToString()!);

        var createRef = await client.PostAsJsonAsync(
            $"/api/commerce/billing-accounts/{billingAccountId}/external-refs",
            new
            {
                hostPlatformKey = hostKey,
                externalTenantId = tenantId,
                externalCustomerRef = (string?)null,
                isPrimary = true
            });
        createRef.EnsureSuccessStatusCode();

        // Snapshot by billing-account id
        var snapResp = await client.GetAsync(
            $"/api/commerce/integration/billing-accounts/{billingAccountId}/entitlement-snapshot");
        snapResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var snap = await snapResp.Content.ReadFromJsonAsync<CommerceEntitlementSnapshot>();
        snap!.BillingAccountId.Should().Be(billingAccountId);
        snap.HostPlatformKey.Should().Be(hostKey);
        snap.ExternalTenantId.Should().Be(tenantId);

        // Snapshot by host-tenant key
        var byTenant = await client.GetAsync(
            $"/api/commerce/integration/host-tenants/{hostKey}/{tenantId}/entitlement-snapshot");
        byTenant.StatusCode.Should().Be(HttpStatusCode.OK);

        // Recommendation
        var recResp = await client.GetAsync(
            $"/api/commerce/integration/billing-accounts/{billingAccountId}/access-recommendation");
        recResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rec = await recResp.Content.ReadFromJsonAsync<AccessRecommendationResponse>();
        rec!.BillingAccountId.Should().Be(billingAccountId);
        rec.AccountStandingStatus.Should().NotBeNullOrEmpty();
    }
}
