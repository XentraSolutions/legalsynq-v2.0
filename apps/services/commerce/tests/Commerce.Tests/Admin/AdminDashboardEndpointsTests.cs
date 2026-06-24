using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Admin;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Admin;

/// <summary>
/// Surface-level checks for the admin dashboard controller. The DB is the
/// shared in-memory fallback (its name is fixed in DI), so other tests in the
/// same run may seed records into it. We therefore assert only on the response
/// shape and HTTP status code — not on absolute totals — to keep these tests
/// deterministic regardless of execution order.
/// </summary>
public class AdminDashboardEndpointsTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;

    public AdminDashboardEndpointsTests(CommerceWebApplicationFactory factory)
        => _factory = factory;

    [Fact]
    public async Task Summary_endpoint_returns_200_with_well_formed_payload()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/admin/dashboard/summary");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AdminDashboardSummaryResponse>();
        body.Should().NotBeNull();
        body!.Catalog.Should().NotBeNull();
        body.BillingAccounts.Should().NotBeNull();
        body.Subscriptions.Should().NotBeNull();
        body.Invoices.Should().NotBeNull();
        body.Payments.Should().NotBeNull();
        body.ProviderEvents.Should().NotBeNull();
        body.GeneratedAtUtc.Should().BeAfter(DateTime.MinValue);
    }

    [Fact]
    public async Task RevenueSummary_endpoint_returns_200_with_currency_array()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/admin/dashboard/revenue-summary");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<RevenueSummaryResponse>();
        body!.ByCurrency.Should().NotBeNull();
    }

    [Fact]
    public async Task AccountStandingSummary_endpoint_returns_200_with_all_status_keys()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/admin/dashboard/account-standing-summary");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AccountStandingSummaryResponse>();
        body!.CountsByStatus.Should().ContainKey("Good");
        body.CountsByStatus.Should().ContainKey("PastDue");
        body.CountsByStatus.Should().ContainKey("Suspended");
    }

    [Fact]
    public async Task ProviderEventSummary_endpoint_returns_200_with_groups_array()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/admin/dashboard/provider-event-summary");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ProviderEventSummaryResponse>();
        body!.Groups.Should().NotBeNull();
        body.TotalEvents.Should().Be(body.Groups.Sum(g => g.Count));
    }

    [Fact]
    public async Task RecentActivity_endpoint_accepts_take_query_string()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/commerce/admin/dashboard/recent-activity?take=15");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<RecentActivityResponse>();
        body!.Entries.Should().NotBeNull();
        body.Entries.Count.Should().BeLessThanOrEqualTo(15);
    }
}
