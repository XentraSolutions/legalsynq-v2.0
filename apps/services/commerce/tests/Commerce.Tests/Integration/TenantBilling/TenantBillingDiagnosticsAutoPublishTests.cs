using Commerce.Application.Integration.Abstractions;
using Commerce.Infrastructure.Integration.TenantBilling;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-03 — diagnostics expose auto-publish posture
/// (AutoPublishEnabled / Capacity / Depth / WorkerRegistered).
/// </summary>
public class TenantBillingDiagnosticsAutoPublishTests
{
    [Fact]
    public async Task Diagnostics_include_autopublish_fields_when_queue_wired()
    {
        var raw = new TenantBillingClientOptions
        {
            Enabled = true,
            BaseUrl = "http://x",
            InternalToken = "t",
            AutoPublishEnabled = true,
            AutoPublishQueueCapacity = 32,
        };
        var monitor = new StaticOptionsMonitor<TenantBillingClientOptions>(raw);
        var breaker = new TenantBillingPublisherCircuitBreaker(monitor, () => DateTimeOffset.UtcNow);
        var queue = new BoundedTenantBillingEntitlementPublishQueue(Options.Create(raw));
        queue.Enqueue(new TenantBillingEntitlementPublishWorkItem(
            Guid.NewGuid(), "subscription-created", DateTime.UtcNow, null));

        var pub = new TenantBillingEntitlementPublisher(
            new HttpClient(new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK, "{}")),
            new FakeSnapshots(), Options.Create(raw),
            breaker, new TenantBillingPublisherMetrics(),
            NullLogger<TenantBillingEntitlementPublisher>.Instance,
            queue);

        var d = await pub.GetDiagnosticsAsync(CancellationToken.None);
        d.AutoPublishEnabled.Should().BeTrue();
        d.AutoPublishQueueCapacity.Should().Be(32);
        d.AutoPublishQueueDepth.Should().Be(1);
        d.WorkerRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task Diagnostics_report_WorkerRegistered_false_when_queue_absent()
    {
        var (pub, _, _, _, _, _) = PublisherTestHelpers.Build();
        var d = await pub.GetDiagnosticsAsync(CancellationToken.None);
        d.WorkerRegistered.Should().BeFalse();
        d.AutoPublishEnabled.Should().BeFalse();
        d.AutoPublishQueueDepth.Should().Be(0);
    }
}
