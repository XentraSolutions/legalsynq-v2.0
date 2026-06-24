using TenantBilling.Domain.Entities;
using Xunit;

namespace TenantBilling.Domain.Tests;

public class InvoiceStatusMachineTests
{
    private static readonly DateTime Now = new(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FutureDue = Now.AddDays(10);
    private static readonly DateTime PastDue = Now.AddDays(-1);

    [Fact]
    public void Issued_with_no_payments_stays_Issued_when_not_past_due()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Issued, totalAmount: 100m, paidSum: 0m, FutureDue, Now);
        Assert.Equal(InvoiceStatus.Issued, s);
    }

    [Fact]
    public void Issued_with_partial_payment_becomes_PartiallyPaid()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Issued, 100m, 40m, FutureDue, Now);
        Assert.Equal(InvoiceStatus.PartiallyPaid, s);
    }

    [Fact]
    public void Issued_with_full_payment_becomes_Paid()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Issued, 100m, 100m, FutureDue, Now);
        Assert.Equal(InvoiceStatus.Paid, s);
    }

    [Fact]
    public void Issued_no_payments_past_due_becomes_Overdue()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Issued, 100m, 0m, PastDue, Now);
        Assert.Equal(InvoiceStatus.Overdue, s);
    }

    [Fact]
    public void Overdue_with_full_payment_becomes_Paid()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Overdue, 100m, 100m, PastDue, Now);
        Assert.Equal(InvoiceStatus.Paid, s);
    }

    [Fact]
    public void Overdue_with_partial_payment_stays_Overdue_when_still_past_due()
    {
        // TBS-B05: a partial payment on a past-due Overdue invoice must not
        // downgrade the invoice back to PartiallyPaid (and silently drop it
        // out of dunning). It stays Overdue until either fully paid or its
        // due date moves into the future (which doesn't happen).
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Overdue, 100m, 25m, PastDue, Now);
        Assert.Equal(InvoiceStatus.Overdue, s);
    }

    [Fact]
    public void PartiallyPaid_past_due_becomes_Overdue()
    {
        // TBS-B05 rule: past-due dominates the partial-payment signal.
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.PartiallyPaid, 100m, 25m, PastDue, Now);
        Assert.Equal(InvoiceStatus.Overdue, s);
    }

    [Fact]
    public void PartiallyPaid_not_past_due_stays_PartiallyPaid()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.PartiallyPaid, 100m, 25m, FutureDue, Now);
        Assert.Equal(InvoiceStatus.PartiallyPaid, s);
    }

    [Fact]
    public void PartiallyPaid_topped_up_to_total_becomes_Paid()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.PartiallyPaid, 100m, 100m, FutureDue, Now);
        Assert.Equal(InvoiceStatus.Paid, s);
    }

    [Fact]
    public void Draft_is_never_auto_transitioned()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Draft, 100m, 100m, PastDue, Now);
        Assert.Equal(InvoiceStatus.Draft, s);
    }

    [Fact]
    public void Voided_is_terminal_and_never_recomputed()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Voided, 100m, 100m, PastDue, Now);
        Assert.Equal(InvoiceStatus.Voided, s);
    }

    [Fact]
    public void Refunded_is_terminal_and_never_recomputed()
    {
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Refunded, 100m, 0m, PastDue, Now);
        Assert.Equal(InvoiceStatus.Refunded, s);
    }

    [Fact]
    public void Due_today_is_not_yet_overdue()
    {
        var dueToday = new DateTime(Now.Year, Now.Month, Now.Day, 23, 59, 0, DateTimeKind.Utc);
        var s = InvoiceStatus.ComputeStatus(InvoiceStatus.Issued, 100m, 0m, dueToday, Now);
        Assert.Equal(InvoiceStatus.Issued, s);
    }

    [Theory]
    [InlineData(InvoiceStatus.Issued, true)]
    [InlineData(InvoiceStatus.PartiallyPaid, true)]
    [InlineData(InvoiceStatus.Overdue, true)]
    [InlineData(InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.Paid, false)]
    [InlineData(InvoiceStatus.Voided, false)]
    [InlineData(InvoiceStatus.Refunded, false)]
    public void AcceptsPayments_only_for_active_billable_states(string status, bool expected)
    {
        Assert.Equal(expected, InvoiceStatus.AcceptsPayments(status));
    }
}
