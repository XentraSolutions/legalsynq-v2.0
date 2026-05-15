using Commerce.Domain.AccountStanding.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Admin;

public class AdminDashboardServiceTests
{
    [Fact]
    public async Task Summary_returns_zeros_for_empty_db()
    {
        using var host = new AdminDashboardTestHost();

        var summary = await host.Service.GetSummaryAsync(default);

        summary.Catalog.Products.Should().Be(0);
        summary.BillingAccounts.Total.Should().Be(0);
        summary.Subscriptions.Total.Should().Be(0);
        summary.Invoices.Total.Should().Be(0);
        summary.Payments.Total.Should().Be(0);
        summary.ProviderEvents.Total.Should().Be(0);
        summary.GeneratedAtUtc.Should().Be(host.Clock.UtcNow);
    }

    [Fact]
    public async Task Summary_aggregates_seeded_counts_correctly()
    {
        using var host = new AdminDashboardTestHost();
        host.SeedAll();

        var summary = await host.Service.GetSummaryAsync(default);

        summary.Catalog.Products.Should().Be(2);
        summary.Catalog.ActiveProducts.Should().Be(1);
        summary.Catalog.Plans.Should().Be(1);
        summary.Catalog.Bundles.Should().Be(1);
        summary.Catalog.Addons.Should().Be(1);
        summary.Catalog.Prices.Should().Be(1);

        summary.BillingAccounts.Total.Should().Be(3);
        summary.BillingAccounts.Active.Should().Be(1);
        summary.BillingAccounts.Suspended.Should().Be(1);
        summary.BillingAccounts.Closed.Should().Be(1);

        summary.Subscriptions.Total.Should().Be(1);
        summary.Subscriptions.Trialing.Should().Be(1);
        summary.Subscriptions.Active.Should().Be(0);

        summary.Invoices.Total.Should().Be(4);
        summary.Invoices.Paid.Should().Be(1);
        summary.Invoices.Open.Should().Be(2);
        summary.Invoices.Draft.Should().Be(1);

        summary.Payments.Total.Should().Be(2);
        summary.Payments.Succeeded.Should().Be(1);
        summary.Payments.Failed.Should().Be(1);

        summary.ProviderEvents.Total.Should().Be(4);
        summary.ProviderEvents.Received.Should().Be(1);
        summary.ProviderEvents.Processed.Should().Be(1);
        summary.ProviderEvents.Failed.Should().Be(1);
        summary.ProviderEvents.Ignored.Should().Be(1);
    }

    [Fact]
    public async Task RevenueSummary_groups_by_currency_paid_vs_outstanding()
    {
        using var host = new AdminDashboardTestHost();
        host.SeedAll();

        var revenue = await host.Service.GetRevenueSummaryAsync(default);

        revenue.ByCurrency.Should().HaveCount(2);
        var usd = revenue.ByCurrency.Single(c => c.Currency == "USD");
        usd.PaidAmountMinor.Should().Be(10000);
        usd.OutstandingAmountMinor.Should().Be(5000);
        usd.PaidInvoiceCount.Should().Be(1);
        usd.OpenInvoiceCount.Should().Be(1);

        var eur = revenue.ByCurrency.Single(c => c.Currency == "EUR");
        eur.PaidAmountMinor.Should().Be(0);
        eur.OutstandingAmountMinor.Should().Be(20000);
        eur.OpenInvoiceCount.Should().Be(1);
    }

    [Fact]
    public async Task RevenueSummary_excludes_draft_void_uncollectible()
    {
        using var host = new AdminDashboardTestHost();
        host.SeedAll();

        var revenue = await host.Service.GetRevenueSummaryAsync(default);

        // Draft USD invoice for 99900 must NOT inflate USD outstanding.
        var usd = revenue.ByCurrency.Single(c => c.Currency == "USD");
        (usd.PaidAmountMinor + usd.OutstandingAmountMinor).Should().Be(15000);
    }

    [Fact]
    public async Task AccountStandingSummary_returns_all_status_keys_even_when_empty()
    {
        using var host = new AdminDashboardTestHost();

        var standing = await host.Service.GetAccountStandingSummaryAsync(default);

        standing.TotalEvaluated.Should().Be(0);
        foreach (AccountStandingStatus s in Enum.GetValues(typeof(AccountStandingStatus)))
        {
            standing.CountsByStatus.Should().ContainKey(s.ToString());
            standing.CountsByStatus[s.ToString()].Should().Be(0);
        }
    }

    [Fact]
    public async Task ProviderEventSummary_groups_and_counts_by_provider_and_status()
    {
        using var host = new AdminDashboardTestHost();
        host.SeedAll();

        var events = await host.Service.GetProviderEventSummaryAsync(default);

        events.TotalEvents.Should().Be(4);
        events.Groups.Should().HaveCount(4);
        events.Groups.Should().OnlyContain(g => g.Provider == "Stripe");
        events.Groups.Should().Contain(g => g.Status == "Failed" && g.Count == 1);
        events.Groups.Should().Contain(g => g.Status == "Ignored" && g.Count == 1);
        events.Groups.Should().Contain(g => g.Status == "Received" && g.Count == 1);
        events.Groups.Should().Contain(g => g.Status == "Processed" && g.Count == 1);
        events.Groups.Should().OnlyContain(g => g.LastEventUtc.HasValue);
    }

    [Fact]
    public async Task RecentActivity_merges_streams_and_caps_take()
    {
        using var host = new AdminDashboardTestHost();
        host.SeedAll();

        var activity = await host.Service.GetRecentActivityAsync(5, default);

        activity.Entries.Should().HaveCount(5);
        // Mixed kinds should be present given seed.
        activity.Entries.Select(e => e.Kind).Distinct().Count().Should().BeGreaterThan(1);
        // Ordered by occurrence descending.
        activity.Entries.Should().BeInDescendingOrder(e => e.OccurredAtUtc);
    }

    [Fact]
    public async Task RecentActivity_clamps_take_to_default_when_zero()
    {
        using var host = new AdminDashboardTestHost();
        host.SeedAll();

        var activity = await host.Service.GetRecentActivityAsync(0, default);

        activity.Entries.Count.Should().BeLessThanOrEqualTo(10);
    }
}
