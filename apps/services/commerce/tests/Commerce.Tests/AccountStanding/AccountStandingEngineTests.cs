using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Domain.Subscriptions.Enums;
using FluentAssertions;
using Xunit;
using AccountStandingPolicyValue = Commerce.Domain.AccountStanding.AccountStandingPolicy;

namespace Commerce.Tests.AccountStanding;

public class AccountStandingEngineTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly AccountStandingPolicyValue Policy = new(GracePeriodDays: 7, PastDueToSuspendedDays: 14);

    private static Subscription MakeSubscription(SubscriptionStatus desired)
    {
        // Subscription.Create starts in Active (no trial dates) or Trialing (trial dates).
        if (desired == SubscriptionStatus.Trialing)
        {
            return Subscription.Create(Guid.NewGuid(), "S-" + Guid.NewGuid().ToString("N")[..6],
                Now, Now, Now.AddMonths(1), Now, Now.AddDays(7), Now);
        }

        var s = Subscription.Create(Guid.NewGuid(), "S-" + Guid.NewGuid().ToString("N")[..6],
            Now, Now, Now.AddMonths(1), null, null, Now);
        switch (desired)
        {
            case SubscriptionStatus.Cancelled:
                s.Cancel(false, "test", Now); break;
            case SubscriptionStatus.Suspended:
                s.Suspend(Now); break;
        }
        return s;
    }

    private static Invoice MakeOpenInvoice(long amount, DateTime? dueDateUtc)
    {
        var inv = Invoice.Create(Guid.NewGuid(), null, "INV-" + Guid.NewGuid().ToString("N")[..6],
            "USD", Now.AddDays(-30), dueDateUtc, InvoiceStatus.Open, Now.AddDays(-30));
        var line = InvoiceLine.Create(inv.Id, null, "Line", 1, amount, "USD", null, null, Now.AddDays(-30));
        inv.Recalculate(new[] { line }, Now.AddDays(-30));
        return inv;
    }

    [Fact]
    public void Closed_account_is_Closed_regardless_of_other_state()
    {
        var (status, _, _, _, _) = Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
            BillingAccountStatus.Closed,
            new List<Subscription>(),
            new List<Invoice>(),
            Now, Policy);
        status.Should().Be(AccountStandingStatus.Closed);
    }

    [Fact]
    public void No_invoices_active_subscription_is_Good()
    {
        var sub = MakeSubscription(SubscriptionStatus.Active);
        var (status, _, _, _, _) = Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
            BillingAccountStatus.Active, new[] { sub }, new List<Invoice>(), Now, Policy);
        status.Should().Be(AccountStandingStatus.Good);
    }

    [Fact]
    public void Past_due_within_grace_window_is_GracePeriod()
    {
        var inv = MakeOpenInvoice(1000, Now.AddDays(-3));
        var (status, _, graceEnd, pastDueSince, _) =
            Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
                BillingAccountStatus.Active, new List<Subscription>(), new[] { inv }, Now, Policy);
        status.Should().Be(AccountStandingStatus.GracePeriod);
        graceEnd.Should().Be(Now.AddDays(-3).AddDays(7));
        pastDueSince.Should().Be(Now.AddDays(-3));
    }

    [Fact]
    public void Past_due_after_grace_window_is_PastDue()
    {
        var inv = MakeOpenInvoice(1000, Now.AddDays(-10));
        var (status, _, _, _, _) =
            Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
                BillingAccountStatus.Active, new List<Subscription>(), new[] { inv }, Now, Policy);
        status.Should().Be(AccountStandingStatus.PastDue);
    }

    [Fact]
    public void Past_due_beyond_suspend_threshold_is_Suspended()
    {
        var inv = MakeOpenInvoice(1000, Now.AddDays(-20));
        var (status, _, _, _, suspended) =
            Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
                BillingAccountStatus.Active, new List<Subscription>(), new[] { inv }, Now, Policy);
        status.Should().Be(AccountStandingStatus.Suspended);
        suspended.Should().Be(Now);
    }

    [Fact]
    public void All_subscriptions_cancelled_with_no_overdue_is_Cancelled()
    {
        var s = MakeSubscription(SubscriptionStatus.Cancelled);
        var (status, _, _, _, _) =
            Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
                BillingAccountStatus.Active, new[] { s }, new List<Invoice>(), Now, Policy);
        status.Should().Be(AccountStandingStatus.Cancelled);
    }

    [Fact]
    public void Trialing_subscription_with_no_overdue_is_Trialing()
    {
        var s = MakeSubscription(SubscriptionStatus.Trialing);
        var (status, _, _, _, _) =
            Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
                BillingAccountStatus.Active, new[] { s }, new List<Invoice>(), Now, Policy);
        status.Should().Be(AccountStandingStatus.Trialing);
    }

    [Fact]
    public void Suspended_account_with_no_invoices_is_Suspended()
    {
        var (status, _, _, _, _) =
            Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
                BillingAccountStatus.Suspended, new List<Subscription>(), new List<Invoice>(), Now, Policy);
        status.Should().Be(AccountStandingStatus.Suspended);
    }

    [Fact]
    public void Open_invoice_with_zero_due_does_not_cause_past_due()
    {
        var inv = Invoice.Create(Guid.NewGuid(), null, "INV-2", "USD", Now.AddDays(-30),
            Now.AddDays(-10), InvoiceStatus.Open, Now.AddDays(-30));
        // Zero-amount line keeps AmountDueMinor at 0.
        var line = InvoiceLine.Create(inv.Id, null, "free", 1, 0, "USD", null, null, Now.AddDays(-30));
        inv.Recalculate(new[] { line }, Now.AddDays(-30));

        var (status, _, _, _, _) =
            Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.Evaluate(
                BillingAccountStatus.Active, new List<Subscription>(), new[] { inv }, Now, Policy);
        status.Should().Be(AccountStandingStatus.Good);
    }
}
