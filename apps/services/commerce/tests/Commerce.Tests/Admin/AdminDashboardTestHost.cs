using Commerce.Application.Common.Time;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Admin.Services;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.Admin;

internal sealed class AdminFixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// Lightweight in-memory test host for the admin dashboard read service.
/// Seeds a small representative dataset across catalog, billing, subscription,
/// invoice, payment, and provider-event entities so every rollup cell has
/// data.
/// </summary>
internal sealed class AdminDashboardTestHost : IDisposable
{
    public CommerceDbContext Db { get; }
    public AdminFixedClock Clock { get; } = new();
    public AdminDashboardService Service { get; }

    public AdminDashboardTestHost()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"admin-tests-{Guid.CreateVersion7()}")
            .Options;
        Db = new CommerceDbContext(opts);
        Service = new AdminDashboardService(Db, Clock);
    }

    public void SeedAll()
    {
        var now = Clock.UtcNow;

        // Catalog: 2 products (1 active), 1 plan (active), 1 bundle, 1 addon, 1 price.
        var p1 = Product.Create("k-p1", "Product 1", null, 0, now);
        p1.Activate(now);
        var p2 = Product.Create("k-p2", "Product 2", null, 0, now);
        var plan = Plan.Create(null, "k-plan", "Plan", null, BillingInterval.Monthly, null, 0, now);
        plan.Activate(now);
        var bundle = Bundle.Create("k-bun", "Bundle", null, now);
        var addon = Addon.Create(null, "k-addon", "Addon", null, now);
        var price = Price.Create(plan.Id, null, null, "USD", 1999, BillingInterval.Monthly, now.AddMinutes(-5), null, now);
        Db.AddRange(p1, p2, plan, bundle, addon, price);
        Db.SaveChanges();

        // Billing: active + suspended + closed.
        var acctActive = BillingAccount.Create("COM-ACC-A1", "Acme A", null, "USD", now);
        acctActive.Activate(now);
        var acctSuspended = BillingAccount.Create("COM-ACC-A2", "Acme B", null, "USD", now);
        acctSuspended.Activate(now);
        acctSuspended.Suspend(now);
        var acctClosed = BillingAccount.Create("COM-ACC-A3", "Acme C", null, "USD", now);
        acctClosed.Activate(now);
        acctClosed.Close(now);
        Db.AddRange(acctActive, acctSuspended, acctClosed);
        Db.SaveChanges();

        // Subscription: 1 trialing-by-default (trial start provided).
        var sub = Subscription.Create(
            acctActive.Id, "COM-SUB-A1",
            now, now, now.AddMonths(1), now, now.AddDays(7), now);
        Db.Subscriptions.Add(sub);
        Db.SaveChanges();

        // Invoices: paid (USD 100), open (USD 50), open (EUR 200), draft (USD 999).
        AddInvoice(acctActive.Id, "USD", 10000, status: InvoiceStatus.Open, fullyPay: true);
        AddInvoice(acctActive.Id, "USD", 5000, status: InvoiceStatus.Open, fullyPay: false);
        AddInvoice(acctActive.Id, "EUR", 20000, status: InvoiceStatus.Open, fullyPay: false);
        AddInvoice(acctActive.Id, "USD", 99900, status: InvoiceStatus.Draft, fullyPay: false);
        Db.SaveChanges();

        // Payments: 1 succeeded, 1 failed.
        var paySucceeded = Payment.Create(
            acctActive.Id, null, null, PaymentProviderType.Stripe,
            null, null, 10000, "USD", PaymentStatus.Pending, now);
        paySucceeded.MarkSucceeded(now);
        var payFailed = Payment.Create(
            acctActive.Id, null, null, PaymentProviderType.Stripe,
            null, null, 5000, "USD", PaymentStatus.Pending, now);
        payFailed.MarkFailed("declined", "card declined", now);
        Db.AddRange(paySucceeded, payFailed);

        // Provider events: 1 received, 1 failed, 1 ignored, 1 processed.
        var ev1 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_1", "invoice.paid", "{}", now);
        var ev2 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_2", "charge.failed", "{}", now);
        ev2.MarkFailed("boom", now);
        var ev3 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_3", "ping", "{}", now);
        ev3.MarkIgnored("ignored", now);
        var ev4 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_4", "invoice.paid", "{}", now);
        ev4.MarkProcessed(now);
        Db.AddRange(ev1, ev2, ev3, ev4);

        Db.SaveChanges();
    }

    private void AddInvoice(Guid accountId, string currency, long amountMinor, InvoiceStatus status, bool fullyPay)
    {
        var inv = Invoice.Create(
            accountId,
            null,
            $"COM-INV-{Guid.CreateVersion7().ToString("N")[..8]}",
            currency,
            Clock.UtcNow,
            Clock.UtcNow.AddDays(7),
            status,
            Clock.UtcNow);
        var line = InvoiceLine.Create(inv.Id, null, "Line", 1, amountMinor, currency, null, null, Clock.UtcNow);
        inv.Recalculate(new[] { line }, Clock.UtcNow);
        Db.Invoices.Add(inv);
        Db.InvoiceLines.Add(line);
        if (fullyPay)
        {
            inv.RegisterPayment(amountMinor, Clock.UtcNow);
        }
    }

    public void Dispose() => Db.Dispose();
}
