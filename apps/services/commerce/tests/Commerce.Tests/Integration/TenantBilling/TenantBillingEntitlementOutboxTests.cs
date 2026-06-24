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
/// TB-INT-04 — outbox repository + processor behaviour.
/// Uses the InMemory provider; the optimistic claim path is
/// exercised single-threaded since the InMemory store does not
/// support transactions or row locks.
/// </summary>
public class TenantBillingEntitlementOutboxTests
{
    private sealed class StubClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class StubPublisher : ITenantBillingEntitlementPublisher
    {
        public List<Guid> Calls { get; } = new();
        public Func<Guid, PublishEntitlementResult>? ResultFor { get; set; }
        public Exception? Throw { get; set; }

        public Task<PublishEntitlementResult> PublishForBillingAccountAsync(Guid ba, CancellationToken ct)
        {
            Calls.Add(ba);
            if (Throw is not null) throw Throw;
            var r = ResultFor?.Invoke(ba) ?? PublishEntitlementResult.Published(ba, Guid.CreateVersion7(), 200, 1);
            return Task.FromResult(r with { BillingAccountId = ba });
        }

        public Task<PublishEntitlementResult> PublishSnapshotAsync(CommerceEntitlementSnapshot s, Guid t, CancellationToken ct)
            => Task.FromResult(PublishEntitlementResult.Published(Guid.Empty, Guid.CreateVersion7(), 200, 1));

        public Task<PreviewEntitlementResult> PreviewForBillingAccountAsync(Guid ba, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<TenantBillingDiagnostics> GetDiagnosticsAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }

    private static CommerceDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"outbox-{Guid.CreateVersion7()}")
            .Options;
        return new CommerceDbContext(opts);
    }

    private static (TenantBillingEntitlementOutboxProcessor proc,
                    EfTenantBillingEntitlementOutbox repo,
                    CommerceDbContext db,
                    StubClock clock,
                    StubPublisher publisher,
                    TenantBillingPublisherMetrics metrics,
                    TenantBillingClientOptions opts)
        Build(Action<TenantBillingClientOptions>? configure = null)
    {
        var raw = new TenantBillingClientOptions
        {
            OutboxEnabled = true,
            OutboxBatchSize = 25,
            OutboxPollSeconds = 1,
            OutboxMaxAttempts = 3,
            OutboxRetryBaseDelaySeconds = 30,
        };
        configure?.Invoke(raw);
        var monitor = Options.Create(raw);
        var clock = new StubClock();
        var db = NewDb();
        var metrics = new TenantBillingPublisherMetrics();
        var publisher = new StubPublisher();
        var repo = new EfTenantBillingEntitlementOutbox(
            db, clock, monitor, metrics, NullLogger<EfTenantBillingEntitlementOutbox>.Instance);
        var proc = new TenantBillingEntitlementOutboxProcessor(
            db, clock, publisher, monitor, metrics,
            NullLogger<TenantBillingEntitlementOutboxProcessor>.Instance);
        return (proc, repo, db, clock, publisher, metrics, raw);
    }

    [Fact]
    public async Task Enqueue_persists_pending_row_and_counts()
    {
        var (_, repo, db, _, _, _, _) = Build();
        var ba = Guid.CreateVersion7();
        var id = await repo.EnqueueAsync(ba, "subscription-created", correlationId: "c1", CancellationToken.None);
        id.Should().NotBe(Guid.Empty);

        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.BillingAccountId.Should().Be(ba);
        row.TriggerSource.Should().Be("subscription-created");
        row.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Pending);
        row.Attempts.Should().Be(0);
        row.MaxAttempts.Should().Be(3);
        row.CorrelationId.Should().Be("c1");

        var counts = await repo.GetCountsAsync(CancellationToken.None);
        counts.Pending.Should().Be(1);
        counts.Published.Should().Be(0);
        counts.Abandoned.Should().Be(0);
    }

    [Fact]
    public async Task Enqueue_returns_empty_for_invalid_input_and_does_not_throw()
    {
        var (_, repo, db, _, _, _, _) = Build();

        var id1 = await repo.EnqueueAsync(Guid.Empty, "subscription-created", null, CancellationToken.None);
        var id2 = await repo.EnqueueAsync(Guid.CreateVersion7(), "  ", null, CancellationToken.None);

        id1.Should().Be(Guid.Empty);
        id2.Should().Be(Guid.Empty);
        (await db.Set<TenantBillingEntitlementPublishOutboxRow>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessDue_publishes_pending_row_and_marks_published()
    {
        var (proc, repo, db, _, publisher, _, _) = Build();
        var ba = Guid.CreateVersion7();
        await repo.EnqueueAsync(ba, "subscription-created", null, CancellationToken.None);

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Considered.Should().Be(1);
        batch.Published.Should().Be(1);
        batch.Retried.Should().Be(0);
        batch.Abandoned.Should().Be(0);
        publisher.Calls.Should().ContainSingle().Which.Should().Be(ba);

        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Published);
        row.Attempts.Should().Be(1);
        row.PublishedAtUtc.Should().NotBeNull();
        row.LockId.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDue_failed_result_schedules_retry_with_linear_backoff()
    {
        var (proc, repo, db, clock, publisher, _, opts) = Build();
        publisher.ResultFor = ba => PublishEntitlementResult.Failed(ba, "transport-error", null, 502, null, 1);
        await repo.EnqueueAsync(Guid.CreateVersion7(), "subscription-created", null, CancellationToken.None);

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Retried.Should().Be(1);
        batch.Abandoned.Should().Be(0);
        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Pending);
        row.Attempts.Should().Be(1);
        row.LastOutcome.Should().Be("failed");
        row.LastReason.Should().Be("transport-error");
        row.LastHttpStatus.Should().Be(502);
        row.NextAttemptAtUtc.Should().Be(clock.UtcNow.AddSeconds(opts.OutboxRetryBaseDelaySeconds * 1));
    }

    [Fact]
    public async Task ProcessDue_failed_after_max_attempts_marks_abandoned()
    {
        var (proc, repo, db, clock, publisher, _, _) = Build(o =>
        {
            o.OutboxMaxAttempts = 2; // first failure → retry, second failure → abandoned
        });
        publisher.ResultFor = ba => PublishEntitlementResult.Failed(ba, "transport-error", null, 502, null, 1);
        await repo.EnqueueAsync(Guid.CreateVersion7(), "subscription-created", null, CancellationToken.None);

        await proc.ProcessDueAsync(10, CancellationToken.None);
        // Move clock past the scheduled retry time so the row is due again.
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Abandoned.Should().Be(1);
        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Abandoned);
        row.Attempts.Should().Be(2);
        row.LastOutcome.Should().Be("failed");
    }

    [Fact]
    public async Task ProcessDue_terminal_skip_reason_marks_abandoned_immediately()
    {
        var (proc, repo, db, _, publisher, _, _) = Build();
        publisher.ResultFor = ba => PublishEntitlementResult.Skipped(ba, "no-external-tenant-id");
        await repo.EnqueueAsync(Guid.CreateVersion7(), "subscription-created", null, CancellationToken.None);

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Abandoned.Should().Be(1);
        batch.Skipped.Should().Be(0);
        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Abandoned);
        row.LastOutcome.Should().Be("skipped");
        row.LastReason.Should().Be("no-external-tenant-id");
    }

    [Fact]
    public async Task ProcessDue_transient_skip_reschedules_without_counting_attempt()
    {
        var (proc, repo, db, clock, publisher, _, opts) = Build();
        publisher.ResultFor = ba => PublishEntitlementResult.Skipped(ba, "publisher-disabled");
        await repo.EnqueueAsync(Guid.CreateVersion7(), "subscription-created", null, CancellationToken.None);

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Skipped.Should().Be(1);
        batch.Abandoned.Should().Be(0);
        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Pending);
        row.Attempts.Should().Be(0, "transient skip must not consume a retry budget");
        row.LastOutcome.Should().Be("skipped");
        row.NextAttemptAtUtc.Should().Be(clock.UtcNow.AddSeconds(opts.OutboxRetryBaseDelaySeconds));
    }

    [Fact]
    public async Task ProcessDue_publisher_exception_schedules_retry()
    {
        var (proc, repo, db, _, publisher, _, _) = Build();
        publisher.Throw = new InvalidOperationException("boom");
        await repo.EnqueueAsync(Guid.CreateVersion7(), "subscription-created", null, CancellationToken.None);

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Retried.Should().Be(1);
        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Pending);
        row.Attempts.Should().Be(1);
        row.LastOutcome.Should().Be("failed");
        row.LastReason.Should().Be("exception");
        row.LastErrorSummary.Should().Be("boom");
    }

    [Fact]
    public async Task ProcessDue_skips_rows_not_yet_due()
    {
        var (proc, repo, db, clock, publisher, _, _) = Build();
        await repo.EnqueueAsync(Guid.CreateVersion7(), "subscription-created", null, CancellationToken.None);
        // Push the row into the future.
        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        typeof(TenantBillingEntitlementPublishOutboxRow)
            .GetProperty(nameof(TenantBillingEntitlementPublishOutboxRow.NextAttemptAtUtc))!
            .SetValue(row, clock.UtcNow.AddMinutes(10));
        await db.SaveChangesAsync();

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Considered.Should().Be(0);
        publisher.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDue_recovers_stale_processing_rows()
    {
        var (proc, repo, db, clock, _, _, opts) = Build();
        await repo.EnqueueAsync(Guid.CreateVersion7(), "subscription-created", null, CancellationToken.None);

        // Manually flip the row to Processing with an old lock so the
        // stale-recovery path on the next poll picks it up.
        var row = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        row.MarkProcessing(Guid.CreateVersion7(), clock.UtcNow);
        await db.SaveChangesAsync();
        // Move clock past the stale threshold (max(poll*3, 60s)).
        var staleSeconds = Math.Max(opts.OutboxPollSeconds * 3, 60);
        clock.UtcNow = clock.UtcNow.AddSeconds(staleSeconds + 5);

        var batch = await proc.ProcessDueAsync(10, CancellationToken.None);

        batch.Recovered.Should().Be(1);
        var refreshed = await db.Set<TenantBillingEntitlementPublishOutboxRow>().SingleAsync();
        // After recovery the row was returned to Pending and (in the
        // same batch) re-claimed and published, so its terminal state
        // here is Published.
        refreshed.Status.Should().Be(TenantBillingEntitlementPublishOutboxStatus.Published);
        refreshed.LockId.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDue_orders_by_NextAttemptAtUtc_and_respects_batch_size()
    {
        var (proc, repo, db, clock, publisher, _, _) = Build();
        var ba1 = Guid.CreateVersion7();
        var ba2 = Guid.CreateVersion7();
        var ba3 = Guid.CreateVersion7();
        await repo.EnqueueAsync(ba1, "t1", null, CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        await repo.EnqueueAsync(ba2, "t2", null, CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        await repo.EnqueueAsync(ba3, "t3", null, CancellationToken.None);

        var batch = await proc.ProcessDueAsync(2, CancellationToken.None);

        batch.Considered.Should().Be(2);
        batch.Published.Should().Be(2);
        publisher.Calls.Should().Equal(ba1, ba2);
    }
}
