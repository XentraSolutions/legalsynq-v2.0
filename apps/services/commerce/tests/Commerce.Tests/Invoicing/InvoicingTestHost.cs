using Commerce.Application.Common.Time;
using Commerce.Application.Invoicing.Abstractions;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Invoicing.Services;
using Commerce.Infrastructure.Payments.Services;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Subscriptions.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using AccountStandingPolicyValue = Commerce.Domain.AccountStanding.AccountStandingPolicy;

namespace Commerce.Tests.Invoicing;

internal sealed class InvFixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class InvoicingTestHost : IDisposable
{
    public CommerceDbContext Db { get; }
    public InvFixedClock Clock { get; } = new();
    public InvoiceNumberGenerator Numbers { get; }
    public InvoiceService Invoices { get; }
    public PaymentRecordingService Recording { get; }
    public PaymentRecordQueryService Records { get; }
    public SubscriptionReconciliationService Reconciliation { get; }
    public Commerce.Infrastructure.AccountStanding.Services.AccountStandingService Standing { get; }

    public InvoicingTestHost()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"inv-tests-{Guid.NewGuid()}")
            .Options;
        Db = new CommerceDbContext(opts);

        Numbers = new InvoiceNumberGenerator(Db);
        var validator = ResolveValidator<Contracts.Invoicing.CreateInvoiceRequest>();
        Invoices = new InvoiceService(Db, Clock, Numbers, validator);
        Recording = new PaymentRecordingService(Db, Clock);
        Records = new PaymentRecordQueryService(Db);
        Reconciliation = new SubscriptionReconciliationService(Db, Clock);
        Standing = new Commerce.Infrastructure.AccountStanding.Services.AccountStandingService(
            Db, Clock, AccountStandingPolicyValue.Default);
    }

    public BillingAccount AddActiveAccount(string number = "COM-ACC-INV01", string currency = "USD")
    {
        var account = BillingAccount.Create(number, "Acme " + Guid.NewGuid().ToString("N")[..6], null, currency, Clock.UtcNow);
        account.Activate(Clock.UtcNow);
        Db.BillingAccounts.Add(account);
        Db.SaveChanges();
        return account;
    }

    public Subscription AddActiveSubscription(BillingAccount acct)
    {
        var plan = Plan.Create(null, "k-" + Guid.NewGuid().ToString("N")[..8], "Plan", null, BillingInterval.Monthly, null, 0, Clock.UtcNow);
        plan.Activate(Clock.UtcNow);
        Db.Plans.Add(plan);
        var price = Price.Create(plan.Id, null, null, "USD", 1999, BillingInterval.Monthly, Clock.UtcNow.AddMinutes(-5), null, Clock.UtcNow);
        price.Activate(Clock.UtcNow);
        Db.Prices.Add(price);
        Db.SaveChanges();

        var sub = Subscription.Create(
            acct.Id,
            "COM-SUB-INV-" + Guid.NewGuid().ToString("N")[..8],
            Clock.UtcNow,
            Clock.UtcNow,
            Clock.UtcNow.AddMonths(1),
            null, null,
            Clock.UtcNow);
        Db.Subscriptions.Add(sub);
        Db.SaveChanges();
        return sub;
    }

    private static IValidator<T> ResolveValidator<T>()
    {
        var asm = typeof(Commerce.Application.DependencyInjection).Assembly;
        var t = asm.GetTypes().First(x => !x.IsAbstract && typeof(IValidator<T>).IsAssignableFrom(x));
        return (IValidator<T>)Activator.CreateInstance(t)!;
    }

    public void Dispose() => Db.Dispose();
}
