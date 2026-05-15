using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-01 / TB-INT-02 — surface tests for the publisher controller
/// at <c>/api/commerce/integration/tenant-billing/*</c>. Uses the
/// default in-memory factory in which the publisher is disabled by
/// config; we verify the publish/preview/diagnostics endpoints
/// without making any real HTTP call to Tenant Billing.
/// </summary>
public class TenantBillingPublisherEndpointTests
    : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;

    public TenantBillingPublisherEndpointTests(CommerceWebApplicationFactory f)
        => _factory = f;

    [Fact]
    public async Task Publish_returns_404_when_billing_account_does_not_exist()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/commerce/integration/tenant-billing/billing-accounts/{Guid.NewGuid()}/publish-entitlement",
            content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Publish_returns_400_when_billing_account_is_empty_guid()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/commerce/integration/tenant-billing/billing-accounts/{Guid.Empty}/publish-entitlement",
            content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Publish_returns_skipped_publisher_disabled_for_existing_account()
    {
        var client = _factory.CreateClient();
        var billingAccountId = await CreateBillingAccountAsync(client);

        var resp = await client.PostAsync(
            $"/api/commerce/integration/tenant-billing/billing-accounts/{billingAccountId}/publish-entitlement",
            content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["outcome"].ToString().Should().Be("skipped");
        body["reason"].ToString().Should().Be("publisher-disabled");
        body["billingAccountId"].ToString().Should().Be(billingAccountId.ToString());
    }

    // ─── TB-INT-02 endpoints ───

    [Fact]
    public async Task Diagnostics_returns_disabled_mode_in_default_factory()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            "/api/commerce/integration/tenant-billing/diagnostics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["mode"].ToString().Should().Be("Disabled");
        body["enabled"].ToString().Should().BeEquivalentTo("False");
        body["targetRoute"].ToString().Should().Be("/api/tenant-billing/entitlements/apply");
        // Internal token must never appear in the response payload.
        var rawJson = await resp.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("internalToken\":\"");
    }

    [Fact]
    public async Task Preview_returns_404_when_billing_account_unknown()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/commerce/integration/tenant-billing/billing-accounts/{Guid.NewGuid()}/preview-entitlement",
            content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_returns_400_when_billing_account_is_empty_guid()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/commerce/integration/tenant-billing/billing-accounts/{Guid.Empty}/preview-entitlement",
            content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Preview_returns_payload_with_skip_reason_when_publisher_disabled()
    {
        var client = _factory.CreateClient();
        var billingAccountId = await CreateBillingAccountAsync(client);

        var resp = await client.PostAsync(
            $"/api/commerce/integration/tenant-billing/billing-accounts/{billingAccountId}/preview-entitlement",
            content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        body!["billingAccountId"].GetString().Should().Be(billingAccountId.ToString());
        body["canPublish"].GetBoolean().Should().BeFalse();
        // No external tenant id was set, so we get the no-external-tenant-id
        // skip reason rather than publisher-disabled (resolution runs first).
        body["skipReason"].GetString().Should().Be("no-external-tenant-id");
        // No HTTP request would have been sent.
    }

    private static async Task<Guid> CreateBillingAccountAsync(HttpClient client)
    {
        var createAcct = await client.PostAsJsonAsync("/api/commerce/billing-accounts", new
        {
            displayName = "TB-INT-02 Test",
            legalName = (string?)null,
            defaultCurrency = "USD"
        });
        createAcct.EnsureSuccessStatusCode();
        var acctBody = await createAcct.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return Guid.Parse(acctBody!["id"].ToString()!);
    }
}
