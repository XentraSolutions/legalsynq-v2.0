using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Subscriptions;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions.Enums;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Commerce.Tests.Subscriptions;

public class SubscriptionServiceTests
{
    private static CreateSubscriptionRequest CreateReq(Guid acc, Guid plan, Guid price, int? trialDays = null)
        => new(acc, plan, price, 1, null, trialDays);

    [Fact]
    public async Task Create_assigns_subscription_number_and_copies_price_fields()
    {
        using var h = new SubscriptionTestHost();
        var account = h.AddActiveAccount();
        var plan = h.AddActivePlan();
        var price = h.AddActivePrice(plan, amountMinor: 4999, currency: "USD");

        var resp = await h.Service.CreateAsync(CreateReq(account.Id, plan.Id, price.Id), default);

        resp.SubscriptionNumber.Should().Be("COM-SUB-000001");
        resp.Status.Should().Be(SubscriptionStatus.Active);
        resp.Items.Should().HaveCount(1);
        resp.Items[0].UnitAmountMinor.Should().Be(4999);
        resp.Items[0].Currency.Should().Be("USD");
        resp.Items[0].BillingInterval.Should().Be(BillingInterval.Monthly);
        resp.CurrentPeriodEndUtc.Should().Be(resp.CurrentPeriodStartUtc.AddMonths(1));
    }

    [Fact]
    public async Task Create_with_trial_days_starts_trialing_and_writes_trial_event()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);

        var resp = await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id, trialDays: 14), default);

        resp.Status.Should().Be(SubscriptionStatus.Trialing);
        resp.TrialEndUtc.Should().Be(resp.TrialStartUtc!.Value.AddDays(14));
        resp.CurrentPeriodStartUtc.Should().Be(resp.TrialEndUtc!.Value);

        var changes = await h.Service.ListChangesAsync(resp.Id, default);
        changes.Select(c => c.ChangeType).Should().Contain(SubscriptionChangeType.Created);
        changes.Select(c => c.ChangeType).Should().Contain(SubscriptionChangeType.TrialStarted);
    }

    [Fact]
    public async Task Create_increments_subscription_number()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);

        var s1 = await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
        var s2 = await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);

        s1.SubscriptionNumber.Should().Be("COM-SUB-000001");
        s2.SubscriptionNumber.Should().Be("COM-SUB-000002");
    }

    [Fact]
    public async Task Create_rejects_closed_billing_account()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        a.Close(h.Clock.UtcNow);
        h.Db.SaveChanges();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);

        var act = async () => await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task Create_rejects_suspended_billing_account()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        a.Suspend(h.Clock.UtcNow);
        h.Db.SaveChanges();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);

        var act = async () => await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task Create_rejects_non_active_plan()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan();
        p.Retire(h.Clock.UtcNow);
        h.Db.SaveChanges();
        var pr = h.AddActivePrice(p);

        var act = async () => await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Create_rejects_non_active_price()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);
        pr.Retire(h.Clock.UtcNow);
        h.Db.SaveChanges();

        var act = async () => await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Create_rejects_price_belonging_to_other_plan()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var planA = h.AddActivePlan(key: "a");
        var planB = h.AddActivePlan(key: "b");
        var priceB = h.AddActivePrice(planB);

        var act = async () => await h.Service.CreateAsync(CreateReq(a.Id, planA.Id, priceB.Id), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Create_rejects_unknown_account()
    {
        using var h = new SubscriptionTestHost();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);
        var act = async () => await h.Service.CreateAsync(CreateReq(Guid.CreateVersion7(), p.Id, pr.Id), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Cancel_immediately_marks_items_cancelled()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        var resp = await h.Service.CancelAsync(sub.Id, new CancelSubscriptionRequest(false, "test"), default);
        resp.Status.Should().Be(SubscriptionStatus.Cancelled);
        resp.Items[0].Status.Should().Be(SubscriptionItemStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_at_period_end_keeps_active_and_raises_flag()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        var resp = await h.Service.CancelAsync(sub.Id, new CancelSubscriptionRequest(true, null), default);
        resp.Status.Should().Be(SubscriptionStatus.Active);
        resp.CancelAtPeriodEnd.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_writes_history_event()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        await h.Service.CancelAsync(sub.Id, new CancelSubscriptionRequest(false, "reason"), default);
        var changes = await h.Service.ListChangesAsync(sub.Id, default);
        changes.Should().Contain(c => c.ChangeType == SubscriptionChangeType.Cancelled && c.Reason == "reason");
    }

    [Fact]
    public async Task Suspend_then_reactivate_round_trip()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);

        var s = await h.Service.SuspendAsync(sub.Id, default);
        s.Status.Should().Be(SubscriptionStatus.Suspended);

        var r = await h.Service.ReactivateAsync(sub.Id, default);
        r.Status.Should().Be(SubscriptionStatus.Active);

        var changes = await h.Service.ListChangesAsync(sub.Id, default);
        changes.Select(c => c.ChangeType).Should().Contain(new[]
        {
            SubscriptionChangeType.Suspended, SubscriptionChangeType.Reactivated
        });
    }

    [Fact]
    public async Task Renew_advances_period_end_by_interval()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        var before = sub.CurrentPeriodEndUtc;

        h.Clock.UtcNow = before;
        var resp = await h.Service.RenewAsync(sub.Id, null, default);
        resp.CurrentPeriodStartUtc.Should().Be(before);
        resp.CurrentPeriodEndUtc.Should().Be(before.AddMonths(1));
    }

    [Fact]
    public async Task Renew_rejected_when_not_active()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        await h.Service.SuspendAsync(sub.Id, default);

        var act = async () => await h.Service.RenewAsync(sub.Id, null, default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task ChangePlan_closes_old_item_and_creates_new()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);

        var newPlan = h.AddActivePlan(key: "ent");
        var newPrice = h.AddActivePrice(newPlan, amountMinor: 19900);

        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(5);
        var resp = await h.Service.ChangePlanAsync(sub.Id, new ChangeSubscriptionPlanRequest(
            newPlan.Id, newPrice.Id, 2, h.Clock.UtcNow, ProrationBehavior.Immediate, "upgrade"), default);

        resp.Items.Should().HaveCount(2);
        resp.Items.Should().Contain(i => i.PlanId == newPlan.Id && i.UnitAmountMinor == 19900 && i.Quantity == 2 && i.Status == SubscriptionItemStatus.Active);
        resp.Items.Should().Contain(i => i.Status == SubscriptionItemStatus.Expired);

        var changes = await h.Service.ListChangesAsync(sub.Id, default);
        changes.Should().Contain(c =>
            c.ChangeType == SubscriptionChangeType.PlanChanged
            && c.ToPlanId == newPlan.Id
            && c.ProrationBehavior == ProrationBehavior.Immediate);
    }

    [Fact]
    public async Task ChangePlan_rejects_price_belonging_to_other_plan()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        var planX = h.AddActivePlan(key: "x");
        var planY = h.AddActivePlan(key: "y");
        var priceY = h.AddActivePrice(planY);

        var act = async () => await h.Service.ChangePlanAsync(sub.Id, new ChangeSubscriptionPlanRequest(
            planX.Id, priceY.Id, null, null, ProrationBehavior.None), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task ChangePlan_rejected_on_cancelled()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        await h.Service.CancelAsync(sub.Id, new CancelSubscriptionRequest(false, null), default);
        var newPlan = h.AddActivePlan(key: "z");
        var newPrice = h.AddActivePrice(newPlan);

        var act = async () => await h.Service.ChangePlanAsync(sub.Id, new ChangeSubscriptionPlanRequest(
            newPlan.Id, newPrice.Id, null, null, ProrationBehavior.None), default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task ChangePlan_rejected_on_suspended()
    {
        using var h = new SubscriptionTestHost();
        var sub = await SeedActive(h);
        await h.Service.SuspendAsync(sub.Id, default);
        var newPlan = h.AddActivePlan(key: "z");
        var newPrice = h.AddActivePrice(newPlan);

        var act = async () => await h.Service.ChangePlanAsync(sub.Id, new ChangeSubscriptionPlanRequest(
            newPlan.Id, newPrice.Id, null, null, ProrationBehavior.None), default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task List_filters_by_billing_account_id()
    {
        using var h = new SubscriptionTestHost();
        var a1 = h.AddActiveAccount("COM-ACC-000001");
        var a2 = h.AddActiveAccount("COM-ACC-000002");
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);

        await h.Service.CreateAsync(CreateReq(a1.Id, p.Id, pr.Id), default);
        await h.Service.CreateAsync(CreateReq(a2.Id, p.Id, pr.Id), default);

        var only1 = await h.Service.ListAsync(a1.Id, default);
        only1.Should().HaveCount(1).And.OnlyContain(s => s.BillingAccountId == a1.Id);

        var all = await h.Service.ListAsync(null, default);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_unknown_throws_NotFound()
    {
        using var h = new SubscriptionTestHost();
        var act = async () => await h.Service.GetAsync(Guid.CreateVersion7(), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_validation_failure_quantity_zero()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);

        var bad = new CreateSubscriptionRequest(a.Id, p.Id, pr.Id, 0);
        var act = async () => await h.Service.CreateAsync(bad, default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Plan_trial_days_is_inherited_when_request_omits_trial()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan(trialDays: 7);
        var pr = h.AddActivePrice(p);
        var resp = await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
        resp.Status.Should().Be(SubscriptionStatus.Trialing);
        resp.TrialEndUtc.Should().Be(resp.TrialStartUtc!.Value.AddDays(7));
    }

    [Fact]
    public async Task Annual_interval_creates_one_year_period()
    {
        using var h = new SubscriptionTestHost();
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan(BillingInterval.Annual);
        var pr = h.AddActivePrice(p);
        var resp = await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
        resp.CurrentPeriodEndUtc.Should().Be(resp.CurrentPeriodStartUtc.AddYears(1));
    }

    private static async Task<Contracts.Subscriptions.SubscriptionResponse> SeedActive(SubscriptionTestHost h)
    {
        var a = h.AddActiveAccount();
        var p = h.AddActivePlan();
        var pr = h.AddActivePrice(p);
        return await h.Service.CreateAsync(CreateReq(a.Id, p.Id, pr.Id), default);
    }
}
