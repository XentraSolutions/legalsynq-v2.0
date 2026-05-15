using Commerce.Application.Common.Time;
using Commerce.Domain.Billing;
using Commerce.Infrastructure.AccountStanding.Services;
using Commerce.Infrastructure.Persistence;
using Commerce.Tests.Integration.TenantBilling;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AccountStandingPolicyValue = Commerce.Domain.AccountStanding.AccountStandingPolicy;

namespace Commerce.Tests.AccountStanding;

/// <summary>
/// TB-INT-03 — verifies that
/// <see cref="AccountStandingService.EvaluateAsync"/> enqueues a
/// single auto-publish work item with the documented trigger label
/// after committing the recalculated standing row.
/// </summary>
public class AccountStandingServiceAutoPublishTriggerTests
{
    private sealed class StandingClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private static (AccountStandingService svc, CommerceDbContext db,
                    StandingClock clock, RecordingPublishQueue queue)
        Build(RecordingPublishQueue? queue = null)
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"as-trigger-{Guid.NewGuid()}")
            .Options;
        var db = new CommerceDbContext(opts);
        var clock = new StandingClock();
        var policy = new AccountStandingPolicyValue(7, 14);
        queue ??= new RecordingPublishQueue();
        var svc = new AccountStandingService(db, clock, policy, publishQueue: queue);
        return (svc, db, clock, queue);
    }

    private static Guid SeedActiveAccount(CommerceDbContext db, DateTime now)
    {
        var acc = BillingAccount.Create("COM-ACC-000001", "Acme", null, "USD", now);
        acc.Activate(now);
        db.BillingAccounts.Add(acc);
        db.SaveChanges();
        return acc.Id;
    }

    [Fact]
    public async Task Evaluate_enqueues_account_standing_recalculated()
    {
        var (svc, db, clock, queue) = Build();
        var ba = SeedActiveAccount(db, clock.UtcNow);

        await svc.EvaluateAsync(ba, CancellationToken.None);

        queue.Enqueued.Should().HaveCount(1);
        queue.Enqueued.TryPeek(out var item).Should().BeTrue();
        item!.BillingAccountId.Should().Be(ba);
        item.TriggerSource.Should().Be("account-standing-recalculated");
        item.EnqueuedAtUtc.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task Evaluate_succeeds_when_AutoPublish_disabled()
    {
        var (svc, db, clock, queue) = Build(new RecordingPublishQueue(autoPublishEnabled: false));
        var ba = SeedActiveAccount(db, clock.UtcNow);

        var resp = await svc.EvaluateAsync(ba, CancellationToken.None);

        resp.BillingAccountId.Should().Be(ba);
        queue.Enqueued.Should().BeEmpty();
    }
}
