using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Billing.Api.Contracts;
using Xunit;

namespace Billing.Tests;

/// <summary>
/// MS-BILL-UI-017 — exercises the new exact-match lookup endpoint
/// <c>GET /api/customers/by-external-reference?value=...</c>. The
/// endpoint is internal-only (covered by X-Internal-Token + X-Tenant-Id
/// middleware tested elsewhere) so these tests focus on the
/// behavioural contract: 200 / 404 / 409 / cross-tenant isolation /
/// soft-delete invisibility / empty-value validation.
/// </summary>
public class CustomersLookupApiTests : IClassFixture<BillingWebApplicationFactory>
{
    private readonly BillingWebApplicationFactory _factory;

    public CustomersLookupApiTests(BillingWebApplicationFactory factory) => _factory = factory;

    private static async Task<CustomerResponse> CreateCustomerAsync(
        HttpClient client,
        string? externalReference)
    {
        var req = new CreateCustomerRequest
        {
            Name = "Acme " + Guid.CreateVersion7().ToString("N")[..6],
            Email = $"acme+{Guid.CreateVersion7():N}@example.com",
            ExternalReference = externalReference,
        };
        var resp = await client.PostAsJsonAsync("/api/customers", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    [Fact]
    public async Task Lookup_returns_200_with_matching_customer_when_unique()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var externalRef = Guid.CreateVersion7().ToString();

        var created = await CreateCustomerAsync(client, externalRef);

        var resp = await client.GetAsync(
            $"/api/customers/by-external-reference?value={Uri.EscapeDataString(externalRef)}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<CustomerResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.TenantId.Should().Be(tenantId);
        body.ExternalReference.Should().Be(externalRef);
    }

    [Fact]
    public async Task Lookup_is_case_insensitive()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var externalRef = "Org-" + Guid.CreateVersion7().ToString("N");

        var created = await CreateCustomerAsync(client, externalRef);

        var resp = await client.GetAsync(
            $"/api/customers/by-external-reference?value={Uri.EscapeDataString(externalRef.ToUpperInvariant())}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await resp.Content.ReadFromJsonAsync<CustomerResponse>())!;
        body.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Lookup_returns_404_when_no_match()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());

        var resp = await client.GetAsync(
            $"/api/customers/by-external-reference?value={Guid.CreateVersion7()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Lookup_returns_400_when_value_missing()
    {
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());

        var missing = await client.GetAsync("/api/customers/by-external-reference");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var blank = await client.GetAsync("/api/customers/by-external-reference?value=");
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var whitespace = await client.GetAsync(
            $"/api/customers/by-external-reference?value={Uri.EscapeDataString("   ")}");
        whitespace.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lookup_is_tenant_scoped()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var clientA = _factory.CreateClientForTenant(tenantA);
        var clientB = _factory.CreateClientForTenant(tenantB);
        var externalRef = Guid.CreateVersion7().ToString();

        await CreateCustomerAsync(clientA, externalRef);

        // tenantB MUST NOT see tenantA's customer even though the
        // externalReference value is identical — it's the tenant id
        // that scopes the query, not the externalReference.
        var crossTenant = await clientB.GetAsync(
            $"/api/customers/by-external-reference?value={Uri.EscapeDataString(externalRef)}");
        crossTenant.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // tenantA still finds it.
        var ownTenant = await clientA.GetAsync(
            $"/api/customers/by-external-reference?value={Uri.EscapeDataString(externalRef)}");
        ownTenant.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lookup_returns_409_when_two_active_customers_share_external_reference()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var externalRef = Guid.CreateVersion7().ToString();

        await CreateCustomerAsync(client, externalRef);
        await CreateCustomerAsync(client, externalRef);

        var resp = await client.GetAsync(
            $"/api/customers/by-external-reference?value={Uri.EscapeDataString(externalRef)}");
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Lookup_excludes_soft_deleted_customers()
    {
        var tenantId = Guid.CreateVersion7();
        var client = _factory.CreateClientForTenant(tenantId);
        var externalRef = Guid.CreateVersion7().ToString();

        var created = await CreateCustomerAsync(client, externalRef);

        // Soft-delete the customer (DELETE → 204).
        var del = await client.DeleteAsync($"/api/customers/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resp = await client.GetAsync(
            $"/api/customers/by-external-reference?value={Uri.EscapeDataString(externalRef)}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Lookup_route_does_not_collide_with_get_by_id()
    {
        // The literal "by-external-reference" must not be parsed as a
        // GUID and routed to GetById — that would return 400 ("id is
        // required") instead of 404. Use a tenant with no customers
        // and an unmatchable value to force the lookup branch.
        var client = _factory.CreateClientForTenant(Guid.CreateVersion7());

        var resp = await client.GetAsync(
            "/api/customers/by-external-reference?value=does-not-exist");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
