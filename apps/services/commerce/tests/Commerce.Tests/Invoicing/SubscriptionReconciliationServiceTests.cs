using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Payments.Enums;
using Commerce.Domain.Subscriptions.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Commerce.Tests.Invoicing;

public class SubscriptionReconciliationServiceTests
{
    private static NormalizedProviderEvent MakeEvent(
        Guid? subscriptionId, NormalizedProviderEventKind kind,
        ProviderSubscriptionStatus? providerStatus = null)
        => new(
            PaymentProviderType.Stripe,
            "evt_" + Guid.CreateVersion7().ToString("N")[..8],
            "evt." + kind,
            kind,
            null, null, null, null, null, null, null, null,
            BillingAccountId: null,
            SubscriptionId: subscriptionId,
            ProviderSubscriptionStatus: providerStatus);

    [Fact]
    public async Task Reconcile_subscription_deleted_cancels_active_and_writes_change()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();
        var sub = host.AddActiveSubscription(account);

        var ev = MakeEvent(sub.Id, NormalizedProviderEventKind.SubscriptionDeleted);
        var changed = await host.Reconciliation.ReconcileFromEventAsync(ev, default);
        await host.Db.SaveChangesAsync();

        changed.Should().BeTrue();
        var refreshed = await host.Db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == sub.Id);
        refreshed.Status.Should().Be(SubscriptionStatus.Cancelled);
        var changes = await host.Db.SubscriptionChanges.AsNoTracking()
            .Where(c => c.SubscriptionId == sub.Id).ToListAsync();
        changes.Should().HaveCount(1);
    }

    [Fact]
    public async Task Reconcile_subscription_deleted_when_already_cancelled_is_noop()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();
        var sub = host.AddActiveSubscription(account);
        sub.Cancel(false, "manual", host.Clock.UtcNow);
        await host.Db.SaveChangesAsync();

        var ev = MakeEvent(sub.Id, NormalizedProviderEventKind.SubscriptionDeleted);
        var changed = await host.Reconciliation.ReconcileFromEventAsync(ev, default);
        changed.Should().BeFalse();
    }

    [Fact]
    public async Task Reconcile_subscription_updated_with_provider_cancelled_cancels_locally()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();
        var sub = host.AddActiveSubscription(account);

        var ev = MakeEvent(sub.Id, NormalizedProviderEventKind.SubscriptionUpdated,
            providerStatus: ProviderSubscriptionStatus.Cancelled);
        var changed = await host.Reconciliation.ReconcileFromEventAsync(ev, default);
        await host.Db.SaveChangesAsync();
        changed.Should().BeTrue();
        var refreshed = await host.Db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == sub.Id);
        refreshed.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task Reconcile_unknown_subscription_returns_false()
    {
        using var host = new InvoicingTestHost();
        var ev = MakeEvent(Guid.CreateVersion7(), NormalizedProviderEventKind.SubscriptionDeleted);
        var changed = await host.Reconciliation.ReconcileFromEventAsync(ev, default);
        changed.Should().BeFalse();
    }
}
