using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Commerce.Infrastructure.Integration.TenantBilling;
using Commerce.Infrastructure.Integration.TenantBilling.Outbox;
using Commerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-04 — additional coverage requested by code review:
/// diagnostics surface, retry-backoff cap at 10×, and exception-path
/// behaviour when the outbox count query fails.
/// </summary>
public class TenantBillingOutboxAdditionalCoverageTests
{
    private sealed class StubClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class StubFailingPublisher : ITenantBillingEntitlementPublisher
    {
        public Task<PublishEntitlementResult> PublishForBillingAccountAsync(Guid ba, CancellationToken ct)
            => Task.FromResult(PublishEntitlementResult.Failed(ba, "transport-error", null, 502, null, 1));
        public Task<PublishEntitlementResult> PublishSnapshotAsync(CommerceEntitlementSnapshot s, Guid t, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<PreviewEntitlementResult?> PreviewForBillingAccountAsync(Guid billingAccountId, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<TenantBillingDiagnostics> GetDiagnosticsAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingOutbox : ITenantBillingEntitlementOutbox
    {
        public Task<Guid> EnqueueAsync(Guid billingAccountId, string triggerSource, string? correlationId, CancellationToken ct)
            => Task.FromResult(Guid.CreateVersion7());
        public Task<TenantBillingEntitlementOutboxCounts> GetCountsAsync(CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task Failed_retry_backoff_caps_multiplier_at_10()
    {
        // Push the row to attempts=11 manually, then process: the next
        // retry should use multiplier=10 (cap), not 12.
        var raw = new TenantBillingClientOptions
        {
            OutboxEnabled = true,
            OutboxMaxAttempts = 50,
            OutboxRetryBaseDelaySeconds = 7,
            OutboxPollSeconds = 10,
        };
        var monitor = Options.Create(raw);
        var clock = new StubClock();
        var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"outbox-cap-{Guid.CreateVersion7()}").Options);
        var metrics = new TenantBillingPublisherMetrics();
        var publisher = new StubFailingPublisher();

        var row = TenantBillingEntitlementPublishOutboxRow.Create(
            Guid.CreateVersion7(), "subscription-created", null, 50, clock.UtcNow);
        // Burn 11 attempts so the next failure schedules with min(12, 10) → 10×.
        for (int i = 0; i < 11; i++)
        {
            row.MarkFailedAndScheduleRetry("transport-error", 502, null, clock.UtcNow, clock.UtcNow);
        }
        db.Add(row);
        await db.SaveChangesAsync();

        var proc = new TenantBillingEntitlementOutboxProcessor(
            db, clock, publisher, monitor, metrics,
            NullLogger<TenantBillingEntitlementOutboxProcessor>.Instance);

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);
        batch.Retried.Should().Be(1);

        var refreshed = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        refreshed.Attempts.Should().Be(12);
        refreshed.NextAttemptAtUtc.Should().Be(clock.UtcNow.AddSeconds(7 * 10),
            "linear backoff multiplier must cap at 10× regardless of attempt count");
    }

    [Fact]
    public async Task Diagnostics_surface_outbox_fields_when_outbox_wired()
    {
        var raw = new TenantBillingClientOptions
        {
            Enabled = true, BaseUrl = "http://x", InternalToken = "t",
            OutboxEnabled = true, OutboxBatchSize = 50, OutboxPollSeconds = 5,
            OutboxMaxAttempts = 7, OutboxRetryBaseDelaySeconds = 12,
        };
        var monitor = new StaticOptionsMonitor<TenantBillingClientOptions>(raw);
        var breaker = new TenantBillingPublisherCircuitBreaker(monitor, () => DateTimeOffset.UtcNow);
        var clock = new StubClock();
        var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"diag-{Guid.CreateVersion7()}").Options);
        var metrics = new TenantBillingPublisherMetrics();
        var outbox = new EfTenantBillingEntitlementOutbox(
            db, clock, Options.Create(raw), metrics,
            NullLogger<EfTenantBillingEntitlementOutbox>.Instance);

        // Seed 3 pending and 1 abandoned to assert grouped counts.
        await outbox.EnqueueAsync(Guid.CreateVersion7(), "t1", null, CancellationToken.None);
        await outbox.EnqueueAsync(Guid.CreateVersion7(), "t2", null, CancellationToken.None);
        await outbox.EnqueueAsync(Guid.CreateVersion7(), "t3", null, CancellationToken.None);
        var abandoned = TenantBillingEntitlementPublishOutboxRow.Create(
            Guid.CreateVersion7(), "t4", null, 1, clock.UtcNow);
        abandoned.MarkAbandoned("failed", "transport-error", 502, null, clock.UtcNow);
        db.Add(abandoned);
        await db.SaveChangesAsync();

        var pub = new TenantBillingEntitlementPublisher(
            new HttpClient(new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK, "{}")),
            new FakeSnapshots(), Options.Create(raw),
            breaker, metrics,
            NullLogger<TenantBillingEntitlementPublisher>.Instance,
            queue: null,
            outbox: outbox);

        var d = await pub.GetDiagnosticsAsync(CancellationToken.None);
        d.OutboxEnabled.Should().BeTrue();
        d.OutboxBatchSize.Should().Be(50);
        d.OutboxPollSeconds.Should().Be(5);
        d.OutboxMaxAttempts.Should().Be(7);
        d.OutboxRetryBaseDelaySeconds.Should().Be(12);
        d.OutboxWorkerRegistered.Should().BeTrue();
        d.OutboxPendingCount.Should().Be(3);
        d.OutboxAbandonedCount.Should().Be(1);
        d.OutboxPublishedCount.Should().Be(0);
        d.OutboxProcessingCount.Should().Be(0);
    }

    [Fact]
    public async Task Diagnostics_swallow_outbox_count_exception_and_report_zeros()
    {
        var raw = new TenantBillingClientOptions
        {
            Enabled = true, BaseUrl = "http://x", InternalToken = "t",
            OutboxEnabled = true,
        };
        var monitor = new StaticOptionsMonitor<TenantBillingClientOptions>(raw);
        var breaker = new TenantBillingPublisherCircuitBreaker(monitor, () => DateTimeOffset.UtcNow);
        var pub = new TenantBillingEntitlementPublisher(
            new HttpClient(new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK, "{}")),
            new FakeSnapshots(), Options.Create(raw),
            breaker, new TenantBillingPublisherMetrics(),
            NullLogger<TenantBillingEntitlementPublisher>.Instance,
            queue: null,
            outbox: new ThrowingOutbox());

        var d = await pub.GetDiagnosticsAsync(CancellationToken.None);
        d.OutboxWorkerRegistered.Should().BeTrue();
        d.OutboxPendingCount.Should().Be(0);
        d.OutboxAbandonedCount.Should().Be(0);
    }
}
