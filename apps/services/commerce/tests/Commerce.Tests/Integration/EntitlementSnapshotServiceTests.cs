using Commerce.Contracts.Integration;
using Commerce.Domain.AccountStanding.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration;

public class EntitlementSnapshotServiceTests
{
    [Fact]
    public async Task Returns_snapshot_by_billing_account()
    {
        using var host = new IntegrationTestHost();
        var seed = host.SeedAccount();

        var snap = await host.Snapshot.GetByBillingAccountAsync(seed.BillingAccountId, false, default);

        snap.Should().NotBeNull();
        snap!.BillingAccountId.Should().Be(seed.BillingAccountId);
        snap.AccountStandingStatus.Should().Be(nameof(AccountStandingStatus.Good));
        snap.AccessRecommendation.Should().Be(AccessRecommendation.Allow);
    }

    [Fact]
    public async Task Returns_snapshot_by_host_tenant()
    {
        using var host = new IntegrationTestHost();
        var seed = host.SeedAccount(hostKey: "Acme-Host", externalTenantId: "tnt-1");

        var snap = await host.Snapshot.GetByHostTenantAsync("acme-host", "tnt-1", false, default);

        snap.Should().NotBeNull();
        snap!.HostPlatformKey.Should().Be("acme-host");
        snap.ExternalTenantId.Should().Be("tnt-1");
    }

    [Fact]
    public async Task Includes_product_plan_feature_and_limit_data()
    {
        using var host = new IntegrationTestHost();
        var seed = host.SeedAccount();

        var snap = await host.Snapshot.GetByBillingAccountAsync(seed.BillingAccountId, false, default);

        snap!.Products.Should().ContainSingle().Which.ProductKey.Should().Be(seed.ProductKey);
        snap.Plans.Should().ContainSingle().Which.PlanKey.Should().Be(seed.PlanKey);
        snap.Plans[0].ProductKey.Should().Be(seed.ProductKey);
        snap.Subscriptions.Should().ContainSingle();
        snap.Subscriptions[0].Items.Should().ContainSingle().Which.PlanKey.Should().Be(seed.PlanKey);

        var limit = snap.Limits.Should().ContainSingle().Subject;
        limit.PlanKey.Should().Be(seed.PlanKey);
        limit.FeatureKey.Should().Be("k-feat");
        limit.IsEnabled.Should().BeTrue();
        limit.LimitValue.Should().Be(100);
    }

    [Fact]
    public async Task Excludes_cancelled_subscriptions_by_default()
    {
        using var host = new IntegrationTestHost();
        var seed = host.SeedAccount(subscriptionShape: SeededSubscriptionShape.Cancelled);

        var snap = await host.Snapshot.GetByBillingAccountAsync(seed.BillingAccountId, false, default);
        snap!.Subscriptions.Should().BeEmpty();

        var snapAll = await host.Snapshot.GetByBillingAccountAsync(seed.BillingAccountId, true, default);
        snapAll!.Subscriptions.Should().ContainSingle();
    }

    [Fact]
    public async Task Returns_null_for_missing_billing_account()
    {
        using var host = new IntegrationTestHost();
        var snap = await host.Snapshot.GetByBillingAccountAsync(Guid.CreateVersion7(), false, default);
        snap.Should().BeNull();
    }

    [Fact]
    public async Task Returns_null_for_missing_host_tenant_mapping()
    {
        using var host = new IntegrationTestHost();
        var snap = await host.Snapshot.GetByHostTenantAsync("unknown", "nope", false, default);
        snap.Should().BeNull();
    }

    [Fact]
    public async Task Snapshot_does_not_expose_payment_provider_event_payloads()
    {
        // Pure structural check: the snapshot DTO has no field that
        // surfaces raw provider payloads / secrets. Captured here to
        // stop drift if anyone tries to add one later.
        var props = typeof(CommerceEntitlementSnapshot)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();
        props.Should().NotContain(p => p.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        props.Should().NotContain(p => p.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        props.Should().NotContain(p => p.Contains("RawJson", StringComparison.OrdinalIgnoreCase));
        props.Should().NotContain(p => p.Equals("StripeEvent", StringComparison.OrdinalIgnoreCase));
    }
}
