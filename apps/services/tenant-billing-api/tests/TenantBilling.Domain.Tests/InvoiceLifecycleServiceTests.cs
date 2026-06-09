using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using Xunit;

namespace TenantBilling.Domain.Tests;

/// <summary>
/// TBS-B05: exhaustive coverage of the centralised state-transition graph.
/// One test per kind of edge so a regression surfaces with a clear failure
/// signal instead of a vague matrix mismatch.
/// </summary>
public class InvoiceLifecycleServiceTests
{
    private static readonly InvoiceLifecycleService Engine = new();

    [Theory]
    // Allowed transitions per spec.
    [InlineData(InvoiceStatus.Draft,             InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Draft,             InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.Issued,            InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Issued,            InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Issued,            InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.Issued,            InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.PartiallyPaid,     InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.PartiallyPaid,     InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.PartiallyPaid,     InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.Overdue,           InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Overdue,           InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Overdue,           InvoiceStatus.Voided)]
    // Refund flow extension already in the domain.
    [InlineData(InvoiceStatus.Paid,              InvoiceStatus.PartiallyRefunded)]
    [InlineData(InvoiceStatus.Paid,              InvoiceStatus.Refunded)]
    [InlineData(InvoiceStatus.PartiallyRefunded, InvoiceStatus.Refunded)]
    public void CanTransition_returns_true_for_allowed_edges(string from, string to)
    {
        Assert.True(Engine.CanTransition(from, to));
        // ValidateTransition must not throw.
        Engine.ValidateTransition(from, to);
    }

    [Theory]
    // Terminal states cannot leave.
    [InlineData(InvoiceStatus.Voided,   InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Voided,   InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Refunded, InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Refunded, InvoiceStatus.Issued)]
    // Paid is terminal except via refund — no Paid → Voided.
    [InlineData(InvoiceStatus.Paid,     InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.Paid,     InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Paid,     InvoiceStatus.Overdue)]
    // No rewinds from later states back to Issued / Draft.
    [InlineData(InvoiceStatus.Issued,        InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Overdue,       InvoiceStatus.Issued)]
    // Draft cannot skip to terminal payment states.
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Overdue)]
    // PartiallyRefunded does not loop back to PartiallyPaid / Paid.
    [InlineData(InvoiceStatus.PartiallyRefunded, InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.PartiallyRefunded, InvoiceStatus.PartiallyPaid)]
    public void CanTransition_returns_false_for_disallowed_edges(string from, string to)
    {
        Assert.False(Engine.CanTransition(from, to));
        Assert.Throws<InvalidInvoiceTransitionException>(() => Engine.ValidateTransition(from, to));
    }

    [Theory]
    [InlineData("Bogus",        InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Issued, "Bogus")]
    [InlineData("",             InvoiceStatus.Issued)]
    public void ValidateTransition_throws_UnknownInvoiceStatusException_for_unknown_statuses(string from, string to)
    {
        Assert.Throws<UnknownInvoiceStatusException>(() => Engine.ValidateTransition(from, to));
    }

    [Theory]
    [InlineData(InvoiceStatus.Voided,   true)]
    [InlineData(InvoiceStatus.Refunded, true)]
    [InlineData(InvoiceStatus.Issued,   false)]
    [InlineData(InvoiceStatus.Paid,     false)]
    [InlineData(InvoiceStatus.Draft,    false)]
    [InlineData(InvoiceStatus.PartiallyPaid, false)]
    [InlineData(InvoiceStatus.PartiallyRefunded, false)]
    public void IsTerminal_matches_invoice_status_helper(string status, bool expected)
    {
        Assert.Equal(expected, Engine.IsTerminal(status));
    }

    [Theory]
    [InlineData(InvoiceStatus.Issued, true)]
    [InlineData(InvoiceStatus.PartiallyPaid, true)]
    [InlineData(InvoiceStatus.Overdue, true)]
    [InlineData(InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.Paid, false)]
    [InlineData(InvoiceStatus.Voided, false)]
    [InlineData(InvoiceStatus.Refunded, false)]
    [InlineData(InvoiceStatus.PartiallyRefunded, false)]
    public void CanAcceptPayment_mirrors_AcceptsPayments(string status, bool expected)
    {
        Assert.Equal(expected, Engine.CanAcceptPayment(status));
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft, true)]
    [InlineData(InvoiceStatus.Issued, true)]
    [InlineData(InvoiceStatus.PartiallyPaid, true)] // structural; payment-existence guard is service layer
    [InlineData(InvoiceStatus.Overdue, true)]
    [InlineData(InvoiceStatus.Paid, false)]
    [InlineData(InvoiceStatus.Voided, false)]
    [InlineData(InvoiceStatus.Refunded, false)]
    [InlineData(InvoiceStatus.PartiallyRefunded, false)]
    public void CanBeVoided_only_for_pre_paid_states(string status, bool expected)
    {
        Assert.Equal(expected, Engine.CanBeVoided(status));
    }

    [Theory]
    [InlineData(InvoiceStatus.Issued, true)]
    [InlineData(InvoiceStatus.PartiallyPaid, true)]
    [InlineData(InvoiceStatus.Overdue, false)] // already overdue — engine has no Overdue → Overdue self-loop
    [InlineData(InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.Paid, false)]
    [InlineData(InvoiceStatus.Voided, false)]
    [InlineData(InvoiceStatus.Refunded, false)]
    [InlineData(InvoiceStatus.PartiallyRefunded, false)]
    public void CanBeMarkedOverdue_only_for_Issued_and_PartiallyPaid(string status, bool expected)
    {
        Assert.Equal(expected, Engine.CanBeMarkedOverdue(status));
    }

    [Fact]
    public void CanTransition_handles_unknown_statuses_without_throwing()
    {
        Assert.False(Engine.CanTransition("Garbage", InvoiceStatus.Issued));
        Assert.False(Engine.CanTransition(InvoiceStatus.Issued, "Garbage"));
        Assert.False(Engine.CanTransition("Garbage", "Garbage"));
    }
}
