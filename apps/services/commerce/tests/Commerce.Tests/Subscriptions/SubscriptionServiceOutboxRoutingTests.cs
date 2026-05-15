using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Subscriptions;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Infrastructure.Integration.TenantBilling;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Subscriptions.Services;
using Commerce.Tests.Integration.TenantBilling;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Commerce.Tests.Subscriptions;

/// <summary>
/// TB-INT-04 — when <c>OutboxEnabled=true</c> and an outbox is wired
/// into the trigger sites, lifecycle commits write to the outbox
/// instead of the in-memory queue. When <c>OutboxEnabled=false</c>
/// the legacy TB-INT-03 queue path is preserved.
/// </summary>
public class SubscriptionServiceOutboxRoutingTests
{
    private sealed class RecordingOutbox : ITenantBillingEntitlementOutbox
    {
        public List<(Guid BillingAccountId, string TriggerSource, string? CorrelationId)> Calls { get; } = new();
        public Func<Guid>? IdFactory { get; set; }
        public Task<Guid> EnqueueAsync(Guid billingAccountId, string triggerSource, string? correlationId, CancellationToken ct)
        {
            Calls.Add((billingAccountId, triggerSource, correlationId));
            return Task.FromResult(IdFactory?.Invoke() ?? Guid.NewGuid());
        }
        public Task<TenantBillingEntitlementOutboxCounts> GetCountsAsync(CancellationToken ct)
            => Task.FromResult(new TenantBillingEntitlementOutboxCounts(0, 0, 0, 0, 0));
    }

    private sealed class RoutingHost : IDisposable
    {
        public CommerceDbContext Db { get; }
        public SubFixedClock Clock { get; } = new();
        public SubscriptionService Service { get; }
        public RecordingPublishQueue Queue { get; }
        public RecordingOutbox Outbox { get; }

        public RoutingHost(bool outboxEnabled, RecordingOutbox? outbox = null)
        {
            var opts = new DbContextOptionsBuilder<CommerceDbContext>()
                .UseInMemoryDatabase($"sub-routing-{Guid.NewGuid()}").Options;
            Db = new CommerceDbContext(opts);

            var appAsm = typeof(Commerce.Application.DependencyInjection).Assembly;
            IValidator<T> Resolve<T>()
            {
                var t = appAsm.GetTypes().First(x => !x.IsAbstract && typeof(IValidator<T>).IsAssignableFrom(x));
                return (IValidator<T>)Activator.CreateInstance(t)!;
            }

            var numbers = new SubscriptionNumberGenerator(Db);
            var history = new SubscriptionChangeWriter(Db, Clock);
            Queue = new RecordingPublishQueue();
            Outbox = outbox ?? new RecordingOutbox();

            var publishOpts = Options.Create(new TenantBillingClientOptions
            {
                AutoPublishEnabled = true,
                OutboxEnabled = outboxEnabled,
            });

            Service = new SubscriptionService(
                Db, Clock, numbers, history,
                Resolve<CreateSubscriptionRequest>(),
                Resolve<ChangeSubscriptionPlanRequest>(),
                Resolve<CancelSubscriptionRequest>(),
                Resolve<RenewSubscriptionRequest>(),
                publishQueue: Queue,
                publishOutbox: Outbox,
                publishOptions: publishOpts);
        }

        public Guid SeedAndCreate()
        {
            var account = BillingAccount.Create("COM-ACC-000001", "Acme", null, "USD", Clock.UtcNow);
            account.Activate(Clock.UtcNow);
            Db.BillingAccounts.Add(account);
            var product = Product.Create("prod", "Pro", null, 1, Clock.UtcNow);
            product.Activate(Clock.UtcNow);
            Db.Products.Add(product);
            var plan = Plan.Create(product.Id, "pro", "Pro", null, BillingInterval.Monthly, null, 1, Clock.UtcNow);
            plan.Activate(Clock.UtcNow);
            Db.Plans.Add(plan);
            var price = Price.Create(plan.Id, null, null, "USD", 9900, BillingInterval.Monthly, Clock.UtcNow, null, Clock.UtcNow);
            price.Activate(Clock.UtcNow);
            Db.Prices.Add(price);
            Db.SaveChanges();
            var resp = Service.CreateAsync(
                new CreateSubscriptionRequest(account.Id, plan.Id, price.Id, 1, null, null),
                CancellationToken.None).GetAwaiter().GetResult();
            return resp.Id;
        }

        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public void Outbox_enabled_routes_create_to_outbox_not_queue()
    {
        using var h = new RoutingHost(outboxEnabled: true);
        h.SeedAndCreate();

        h.Outbox.Calls.Should().ContainSingle().Which.TriggerSource.Should().Be("subscription-created");
        h.Queue.Enqueued.Should().BeEmpty("outbox path must skip the in-memory queue when enabled");
    }

    [Fact]
    public void Outbox_disabled_falls_back_to_in_memory_queue()
    {
        using var h = new RoutingHost(outboxEnabled: false);
        h.SeedAndCreate();

        h.Queue.Enqueued.Should().HaveCount(1)
            .And.Subject.Should().AllSatisfy(i => i.TriggerSource.Should().Be("subscription-created"));
        h.Outbox.Calls.Should().BeEmpty("outbox must not be touched when disabled");
    }
}
