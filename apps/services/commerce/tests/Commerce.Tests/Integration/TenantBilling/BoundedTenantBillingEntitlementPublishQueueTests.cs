using Commerce.Application.Integration.Abstractions;
using Commerce.Infrastructure.Integration.TenantBilling;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

public class BoundedTenantBillingEntitlementPublishQueueTests
{
    private static BoundedTenantBillingEntitlementPublishQueue Build(
        bool autoPublishEnabled = true,
        int capacity = 4)
    {
        var opts = new TenantBillingClientOptions
        {
            AutoPublishEnabled = autoPublishEnabled,
            AutoPublishQueueCapacity = capacity,
        };
        return new BoundedTenantBillingEntitlementPublishQueue(Options.Create(opts));
    }

    private static TenantBillingEntitlementPublishWorkItem Item(string source = "subscription-created")
        => new(Guid.CreateVersion7(), source, DateTime.UtcNow, null);

    [Fact]
    public void Capacity_is_clamped_and_reported()
    {
        var q = Build(capacity: 0); // clamps up to 1
        q.Capacity.Should().Be(1);
        q.Depth.Should().Be(0);
        q.AutoPublishEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enqueue_returns_SkippedDisabled_when_AutoPublishEnabled_false()
    {
        var q = Build(autoPublishEnabled: false);
        q.Enqueue(Item()).Should().Be(EnqueueResult.SkippedDisabled);
        q.Depth.Should().Be(0);
    }

    [Fact]
    public void Enqueue_returns_Invalid_for_empty_billingAccountId_or_source()
    {
        var q = Build();
        q.Enqueue(new TenantBillingEntitlementPublishWorkItem(
                Guid.Empty, "x", DateTime.UtcNow, null))
            .Should().Be(EnqueueResult.Invalid);
        q.Enqueue(new TenantBillingEntitlementPublishWorkItem(
                Guid.CreateVersion7(), "  ", DateTime.UtcNow, null))
            .Should().Be(EnqueueResult.Invalid);
        q.Depth.Should().Be(0);
    }

    [Fact]
    public void Enqueue_accepts_until_capacity_then_drops()
    {
        var q = Build(capacity: 3);
        q.Enqueue(Item()).Should().Be(EnqueueResult.Accepted);
        q.Enqueue(Item()).Should().Be(EnqueueResult.Accepted);
        q.Enqueue(Item()).Should().Be(EnqueueResult.Accepted);
        q.Depth.Should().Be(3);

        // Channel is full; subsequent writes return DroppedQueueFull
        // immediately (non-blocking).
        q.Enqueue(Item()).Should().Be(EnqueueResult.DroppedQueueFull);
        q.Enqueue(Item()).Should().Be(EnqueueResult.DroppedQueueFull);
        q.Depth.Should().Be(3);
    }

    [Fact]
    public async Task Enqueue_allows_duplicate_billing_account_ids()
    {
        var q = Build(capacity: 4);
        var ba = Guid.CreateVersion7();
        q.Enqueue(new TenantBillingEntitlementPublishWorkItem(ba, "subscription-created", DateTime.UtcNow, null))
            .Should().Be(EnqueueResult.Accepted);
        q.Enqueue(new TenantBillingEntitlementPublishWorkItem(ba, "subscription-activated", DateTime.UtcNow, null))
            .Should().Be(EnqueueResult.Accepted);
        q.Depth.Should().Be(2);

        // Drain via ReadAllAsync to prove items are surfaced.
        q.GetType().GetMethod("Complete",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(q, null);
        var items = new List<TenantBillingEntitlementPublishWorkItem>();
        await foreach (var i in q.ReadAllAsync(CancellationToken.None))
        {
            items.Add(i);
        }
        items.Should().HaveCount(2);
        items[0].TriggerSource.Should().Be("subscription-created");
        items[1].TriggerSource.Should().Be("subscription-activated");
    }
}
