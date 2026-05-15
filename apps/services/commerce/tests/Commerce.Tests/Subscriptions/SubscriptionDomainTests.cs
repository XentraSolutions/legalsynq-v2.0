using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Domain.Subscriptions.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Subscriptions;

public class SubscriptionDomainTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Subscription New(DateTime? trialStart = null, DateTime? trialEnd = null)
        => Subscription.Create(
            Guid.NewGuid(), "COM-SUB-000001", Now, Now, Now.AddMonths(1),
            trialStart, trialEnd, Now);

    [Fact]
    public void Create_without_trial_starts_as_active()
    {
        var sub = New();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.TrialStartUtc.Should().BeNull();
    }

    [Fact]
    public void Create_with_trial_starts_as_trialing()
    {
        var sub = New(Now, Now.AddDays(14));
        sub.Status.Should().Be(SubscriptionStatus.Trialing);
        sub.TrialEndUtc.Should().Be(Now.AddDays(14));
    }

    [Fact]
    public void Create_rejects_inverted_period()
    {
        var act = () => Subscription.Create(Guid.NewGuid(), "n", Now, Now.AddDays(2), Now, null, null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_rejects_one_sided_trial()
    {
        var act = () => Subscription.Create(Guid.NewGuid(), "n", Now, Now, Now.AddMonths(1), Now, null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Trialing_can_activate_to_active()
    {
        var sub = New(Now, Now.AddDays(7));
        sub.Activate(Now.AddDays(7));
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Cancelled_cannot_activate()
    {
        var sub = New();
        sub.Cancel(false, null, Now.AddDays(1));
        var act = () => sub.Activate(Now.AddDays(2));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_immediately_clears_status()
    {
        var sub = New();
        sub.Cancel(false, "user request", Now.AddDays(1));
        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.CancelledAtUtc.Should().Be(Now.AddDays(1));
        sub.CancellationReason.Should().Be("user request");
    }

    [Fact]
    public void Cancel_at_period_end_keeps_status_and_raises_flag()
    {
        var sub = New();
        sub.Cancel(true, null, Now.AddDays(1));
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CancelAtPeriodEnd.Should().BeTrue();
    }

    [Fact]
    public void Suspend_only_from_active()
    {
        var sub = New();
        sub.Suspend(Now.AddDays(1));
        sub.Status.Should().Be(SubscriptionStatus.Suspended);

        var bad = () => sub.Suspend(Now.AddDays(2));
        bad.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reactivate_only_from_suspended()
    {
        var sub = New();
        var bad = () => sub.Reactivate(Now);
        bad.Should().Throw<InvalidOperationException>();

        sub.Suspend(Now.AddDays(1));
        sub.Reactivate(Now.AddDays(2));
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Renew_only_when_active()
    {
        var sub = New();
        sub.Renew(Now.AddMonths(1), Now.AddMonths(2), Now.AddMonths(1));
        sub.CurrentPeriodEndUtc.Should().Be(Now.AddMonths(2));

        sub.Suspend(Now.AddMonths(1).AddDays(1));
        var bad = () => sub.Renew(Now.AddMonths(2), Now.AddMonths(3), Now);
        bad.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SubscriptionItem_create_rejects_zero_quantity()
    {
        var act = () => SubscriptionItem.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            0, 100, "USD", BillingInterval.Monthly, Now, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SubscriptionItem_close_must_be_after_effective_from()
    {
        var item = SubscriptionItem.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, 100, "USD", BillingInterval.Monthly, Now, Now);
        var act = () => item.Close(Now, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BillingPeriodCalculator_handles_intervals()
    {
        BillingPeriodCalculator.NextPeriodEnd(Now, BillingInterval.Monthly).Should().Be(Now.AddMonths(1));
        BillingPeriodCalculator.NextPeriodEnd(Now, BillingInterval.Annual).Should().Be(Now.AddYears(1));
        var oneTime = BillingPeriodCalculator.NextPeriodEnd(Now, BillingInterval.OneTime);
        oneTime.Should().BeAfter(Now.AddYears(50));

        var act = () => BillingPeriodCalculator.NextPeriodEnd(Now, BillingInterval.Custom);
        act.Should().Throw<InvalidOperationException>();
    }
}
