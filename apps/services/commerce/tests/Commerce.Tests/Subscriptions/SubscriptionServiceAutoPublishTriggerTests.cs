using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Subscriptions;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Subscriptions.Services;
using Commerce.Tests.Integration.TenantBilling;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Commerce.Tests.Subscriptions;

/// <summary>
/// TB-INT-03 — verifies that each subscription lifecycle commit
/// enqueues exactly one auto-publish work item with the documented
/// trigger label, and that Commerce operations succeed even when the
/// queue is disabled or refuses writes.
/// </summary>
public class SubscriptionServiceAutoPublishTriggerTests
{
    private sealed class TriggerHost : IDisposable
    {
        public CommerceDbContext Db { get; }
        public SubFixedClock Clock { get; } = new();
        public SubscriptionService Service { get; }
        public RecordingPublishQueue Queue { get; }

        public TriggerHost(RecordingPublishQueue? queue = null)
        {
            var opts = new DbContextOptionsBuilder<CommerceDbContext>()
                .UseInMemoryDatabase($"sub-trigger-{Guid.NewGuid()}")
                .Options;
            Db = new CommerceDbContext(opts);

            var appAsm = typeof(Commerce.Application.DependencyInjection).Assembly;
            IValidator<T> Resolve<T>()
            {
                var validatorType = appAsm.GetTypes()
                    .First(t => !t.IsAbstract && typeof(IValidator<T>).IsAssignableFrom(t));
                return (IValidator<T>)Activator.CreateInstance(validatorType)!;
            }

            var numbers = new SubscriptionNumberGenerator(Db);
            var history = new SubscriptionChangeWriter(Db, Clock);
            Queue = queue ?? new RecordingPublishQueue();

            Service = new SubscriptionService(Db, Clock, numbers, history,
                Resolve<CreateSubscriptionRequest>(),
                Resolve<ChangeSubscriptionPlanRequest>(),
                Resolve<CancelSubscriptionRequest>(),
                Resolve<RenewSubscriptionRequest>(),
                publishQueue: Queue);
        }

        public BillingAccount AddActiveAccount()
        {
            var acc = BillingAccount.Create("COM-ACC-000001", "Acme", null, "USD", Clock.UtcNow);
            acc.Activate(Clock.UtcNow);
            Db.BillingAccounts.Add(acc);
            Db.SaveChanges();
            return acc;
        }

        public Plan AddActivePlan()
        {
            var product = Product.Create("prod", "Pro", null, 1, Clock.UtcNow);
            product.Activate(Clock.UtcNow);
            Db.Products.Add(product);
            var plan = Plan.Create(product.Id, "pro", "Pro", null,
                BillingInterval.Monthly, null, 1, Clock.UtcNow);
            plan.Activate(Clock.UtcNow);
            Db.Plans.Add(plan);
            Db.SaveChanges();
            return plan;
        }

        public Price AddActivePrice(Plan plan)
        {
            var price = Price.Create(plan.Id, null, null, "USD", 9900,
                BillingInterval.Monthly, Clock.UtcNow, null, Clock.UtcNow);
            price.Activate(Clock.UtcNow);
            Db.Prices.Add(price);
            Db.SaveChanges();
            return price;
        }

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Guid> CreateAsync(TriggerHost h)
    {
        var account = h.AddActiveAccount();
        var plan = h.AddActivePlan();
        var price = h.AddActivePrice(plan);
        var resp = await h.Service.CreateAsync(
            new CreateSubscriptionRequest(account.Id, plan.Id, price.Id, 1, null, null),
            CancellationToken.None);
        return resp.Id;
    }

    [Fact]
    public async Task Create_enqueues_subscription_created()
    {
        using var h = new TriggerHost();
        await CreateAsync(h);

        h.Queue.Enqueued.Should().HaveCount(1);
        h.Queue.Enqueued.TryPeek(out var item).Should().BeTrue();
        item!.TriggerSource.Should().Be("subscription-created");
        item.EnqueuedAtUtc.Should().Be(h.Clock.UtcNow);
    }

    [Fact]
    public async Task Lifecycle_each_commit_enqueues_once_with_correct_label()
    {
        using var h = new TriggerHost();
        var id = await CreateAsync(h);

        h.Queue.Enqueued.Clear();

        await h.Service.SuspendAsync(id, CancellationToken.None);
        await h.Service.ReactivateAsync(id, CancellationToken.None);
        await h.Service.CancelAsync(id,
            new CancelSubscriptionRequest(false, "test"), CancellationToken.None);

        var labels = h.Queue.Enqueued.Select(x => x.TriggerSource).ToArray();
        labels.Should().Equal(
            "subscription-suspended",
            "subscription-reactivated",
            "subscription-cancelled");
    }

    [Fact]
    public async Task Activate_from_trialing_enqueues_subscription_activated()
    {
        // Create with trialDays=7 so the subscription starts as Trialing,
        // then call ActivateAsync to exercise the documented label.
        using var h = new TriggerHost();
        var account = h.AddActiveAccount();
        var plan = h.AddActivePlan();
        var price = h.AddActivePrice(plan);
        var resp = await h.Service.CreateAsync(
            new CreateSubscriptionRequest(account.Id, plan.Id, price.Id, 1, null, 7, null),
            CancellationToken.None);

        h.Queue.Enqueued.Clear();

        await h.Service.ActivateAsync(resp.Id, CancellationToken.None);

        var labels = h.Queue.Enqueued.Select(x => x.TriggerSource).ToArray();
        labels.Should().Equal("subscription-activated");
    }

    [Fact]
    public async Task ChangePlan_enqueues_subscription_plan_changed()
    {
        using var h = new TriggerHost();
        var id = await CreateAsync(h);

        // Add a second active price on the same plan to swap into.
        var plan = h.Db.Plans.First();
        var newPrice = Price.Create(plan.Id, null, null, "USD", 12900,
            BillingInterval.Monthly, h.Clock.UtcNow, null, h.Clock.UtcNow);
        newPrice.Activate(h.Clock.UtcNow);
        h.Db.Prices.Add(newPrice);
        await h.Db.SaveChangesAsync();

        h.Queue.Enqueued.Clear();

        // EffectiveAtUtc=null → service defaults to clock.UtcNow; the
        // validator's IsReasonablyRecent treats null as acceptable, so
        // this dodges the wall-clock vs fixed-clock drift unrelated to
        // TB-INT-03.
        await h.Service.ChangePlanAsync(id,
            new ChangeSubscriptionPlanRequest(
                plan.Id, newPrice.Id, null, null,
                ProrationBehavior.None, null, null),
            CancellationToken.None);

        var labels = h.Queue.Enqueued.Select(x => x.TriggerSource).ToArray();
        labels.Should().Equal("subscription-plan-changed");
    }

    [Fact]
    public async Task Renew_does_NOT_enqueue()
    {
        // Renew is intentionally NOT wired to auto-publish; entitlements
        // do not change at period rollover. AccountStanding picks up
        // any standing change separately.
        using var h = new TriggerHost();
        var id = await CreateAsync(h);
        // Move sub to Active to make Renew valid (fresh subs are Active).
        h.Queue.Enqueued.Clear();

        await h.Service.RenewAsync(id, new RenewSubscriptionRequest(), CancellationToken.None);

        h.Queue.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_succeeds_when_AutoPublish_disabled()
    {
        using var h = new TriggerHost(new RecordingPublishQueue(autoPublishEnabled: false));
        var id = await CreateAsync(h);
        id.Should().NotBe(Guid.Empty);
        h.Queue.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_succeeds_when_queue_is_full()
    {
        using var h = new TriggerHost(
            new RecordingPublishQueue(resultToReturn: EnqueueResult.DroppedQueueFull));
        var id = await CreateAsync(h);
        id.Should().NotBe(Guid.Empty,
            "queue-full must not roll back the Commerce commit");
    }
}
