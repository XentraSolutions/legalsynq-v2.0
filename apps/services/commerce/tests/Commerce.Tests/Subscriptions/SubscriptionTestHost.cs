using Commerce.Application.Common.Time;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Subscriptions.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.Subscriptions;

internal sealed class SubFixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
}

internal sealed class SubscriptionTestHost : IDisposable
{
    public CommerceDbContext Db { get; }
    public SubFixedClock Clock { get; } = new();
    public SubscriptionService Service { get; }

    public SubscriptionTestHost()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"sub-tests-{Guid.NewGuid()}")
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

        Service = new SubscriptionService(Db, Clock, numbers, history,
            Resolve<Contracts.Subscriptions.CreateSubscriptionRequest>(),
            Resolve<Contracts.Subscriptions.ChangeSubscriptionPlanRequest>(),
            Resolve<Contracts.Subscriptions.CancelSubscriptionRequest>(),
            Resolve<Contracts.Subscriptions.RenewSubscriptionRequest>());
    }

    public BillingAccount AddActiveAccount(string number = "COM-ACC-000001")
    {
        var account = BillingAccount.Create(number, "Acme", null, "USD", Clock.UtcNow);
        account.Activate(Clock.UtcNow);
        Db.BillingAccounts.Add(account);
        Db.SaveChanges();
        return account;
    }

    public Plan AddActivePlan(BillingInterval interval = BillingInterval.Monthly, int? trialDays = null, string key = "pro")
    {
        var plan = Plan.Create(null, key, "Pro", null, interval, trialDays, 0, Clock.UtcNow);
        plan.Activate(Clock.UtcNow);
        Db.Plans.Add(plan);
        Db.SaveChanges();
        return plan;
    }

    public Price AddActivePrice(Plan plan, long amountMinor = 9900, string currency = "USD")
    {
        var price = Price.Create(plan.Id, null, null, currency, amountMinor, plan.BillingInterval, Clock.UtcNow, null, Clock.UtcNow);
        price.Activate(Clock.UtcNow);
        Db.Prices.Add(price);
        Db.SaveChanges();
        return price;
    }

    public void Dispose() => Db.Dispose();
}
