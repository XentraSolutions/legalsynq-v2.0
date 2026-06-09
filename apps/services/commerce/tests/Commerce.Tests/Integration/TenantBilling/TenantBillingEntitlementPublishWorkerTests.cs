using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Commerce.Infrastructure.Integration.TenantBilling;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

public class TenantBillingEntitlementPublishWorkerTests
{
    /// <summary>
    /// Counting publisher that records every call and lets the test
    /// pin the outcome it returns.
    /// </summary>
    private sealed class CountingPublisher : ITenantBillingEntitlementPublisher
    {
        public List<Guid> Calls { get; } = new();
        public PublishEntitlementResult Result { get; set; } = PublishEntitlementResult.Skipped(
            Guid.Empty, "publisher-disabled");
        public Exception? Throw { get; set; }

        public Task<PublishEntitlementResult> PublishForBillingAccountAsync(Guid ba, CancellationToken ct)
        {
            Calls.Add(ba);
            if (Throw is not null) throw Throw;
            return Task.FromResult(Result with { BillingAccountId = ba });
        }
        public Task<PublishEntitlementResult> PublishSnapshotAsync(CommerceEntitlementSnapshot s, Guid t, CancellationToken ct)
            => Task.FromResult(Result);
        public Task<PreviewEntitlementResult> PreviewForBillingAccountAsync(Guid ba, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<TenantBillingDiagnostics> GetDiagnosticsAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }

    private static (TenantBillingEntitlementPublishWorker worker,
                    BoundedTenantBillingEntitlementPublishQueue queue,
                    CountingPublisher publisher,
                    TenantBillingPublisherMetrics metrics)
        Build(int capacity = 16, bool autoPublish = true,
              PublishEntitlementOutcome outcome = PublishEntitlementOutcome.Published,
              Exception? throwOnPublish = null)
    {
        var opts = Options.Create(new TenantBillingClientOptions
        {
            AutoPublishEnabled = autoPublish, AutoPublishQueueCapacity = capacity,
        });
        var queue = new BoundedTenantBillingEntitlementPublishQueue(opts);
        var publisher = new CountingPublisher
        {
            Result = outcome switch
            {
                PublishEntitlementOutcome.Published => PublishEntitlementResult.Published(
                    Guid.Empty, Guid.CreateVersion7(), 200, 1),
                PublishEntitlementOutcome.Skipped => PublishEntitlementResult.Skipped(
                    Guid.Empty, "publisher-disabled"),
                _ => PublishEntitlementResult.Failed(Guid.Empty, "transport-error", null, 502, null, 2),
            },
            Throw = throwOnPublish,
        };
        var services = new ServiceCollection();
        services.AddSingleton<ITenantBillingEntitlementPublisher>(publisher);
        var sp = services.BuildServiceProvider();
        var metrics = new TenantBillingPublisherMetrics();
        var worker = new TenantBillingEntitlementPublishWorker(
            queue, sp.GetRequiredService<IServiceScopeFactory>(), metrics,
            NullLogger<TenantBillingEntitlementPublishWorker>.Instance);
        return (worker, queue, publisher, metrics);
    }

    private static async Task RunWorkerUntilDrainedAsync(
        TenantBillingEntitlementPublishWorker worker,
        BoundedTenantBillingEntitlementPublishQueue queue,
        Func<bool> doneWhen,
        TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        var task = worker.StartAsync(cts.Token);
        // Wait for the loop to start, then poll until done.
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!doneWhen() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
        // Complete the channel + stop the worker to exit ReadAllAsync.
        typeof(BoundedTenantBillingEntitlementPublishQueue)
            .GetMethod("Complete",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(queue, null);
        await worker.StopAsync(CancellationToken.None);
        await task;
    }

    [Fact]
    public async Task Worker_processes_each_enqueued_item_in_order()
    {
        var (worker, queue, publisher, metrics) = Build();
        var ba1 = Guid.CreateVersion7();
        var ba2 = Guid.CreateVersion7();
        queue.Enqueue(new(ba1, "subscription-created", DateTime.UtcNow, null));
        queue.Enqueue(new(ba2, "account-standing-recalculated", DateTime.UtcNow, null));

        await RunWorkerUntilDrainedAsync(worker, queue, () => publisher.Calls.Count == 2);

        publisher.Calls.Should().Equal(ba1, ba2);
    }

    [Fact]
    public async Task Worker_swallows_publisher_exception_and_keeps_going()
    {
        var (worker, queue, publisher, _) = Build(throwOnPublish: new InvalidOperationException("boom"));
        queue.Enqueue(new(Guid.CreateVersion7(), "subscription-activated", DateTime.UtcNow, null));
        queue.Enqueue(new(Guid.CreateVersion7(), "subscription-cancelled", DateTime.UtcNow, null));

        await RunWorkerUntilDrainedAsync(worker, queue, () => publisher.Calls.Count == 2);

        publisher.Calls.Should().HaveCount(2,
            "an exception on item 1 must NOT prevent item 2 from being processed");
    }

    [Fact]
    public async Task Worker_handles_skipped_outcome_without_failing()
    {
        var (worker, queue, publisher, _) = Build(outcome: PublishEntitlementOutcome.Skipped);
        queue.Enqueue(new(Guid.CreateVersion7(), "subscription-suspended", DateTime.UtcNow, null));
        await RunWorkerUntilDrainedAsync(worker, queue, () => publisher.Calls.Count == 1);
        publisher.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Worker_exits_cleanly_on_cancellation()
    {
        var (worker, queue, publisher, _) = Build();
        using var cts = new CancellationTokenSource();
        var task = worker.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        // Stop must complete promptly without throwing.
        await worker.StopAsync(CancellationToken.None);
        publisher.Calls.Should().BeEmpty();
        await task;
    }
}
