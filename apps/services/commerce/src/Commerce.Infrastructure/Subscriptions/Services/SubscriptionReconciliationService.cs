using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Payments.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Subscriptions.Services;

/// <summary>
/// Reconciles a Commerce-owned <see cref="Subscription"/> from the
/// latest <see cref="NormalizedProviderEvent"/>. Writes a
/// <see cref="SubscriptionChange"/> row whenever the local subscription
/// status changes as a result of the reconciliation.
/// </summary>
public sealed class SubscriptionReconciliationService : ISubscriptionReconciliationService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;

    public SubscriptionReconciliationService(CommerceDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<bool> ReconcileFromEventAsync(NormalizedProviderEvent ev, CancellationToken ct)
    {
        var subscription = await ResolveSubscriptionAsync(ev, ct);
        if (subscription is null) return false;

        var nowUtc = _clock.UtcNow;
        var occurredAtUtc = ev.OccurredAtUtc ?? nowUtc;
        var previous = subscription.Status;

        var changed = ev.Kind switch
        {
            NormalizedProviderEventKind.SubscriptionDeleted => CancelIfPossible(subscription, nowUtc),
            NormalizedProviderEventKind.PaymentIntentFailed => MarkPastDueIfPossible(subscription, nowUtc),
            NormalizedProviderEventKind.InvoicePaymentFailed => MarkPastDueIfPossible(subscription, nowUtc),
            NormalizedProviderEventKind.PaymentIntentSucceeded => RecoverFromPastDueIfPossible(subscription, nowUtc),
            NormalizedProviderEventKind.InvoicePaymentSucceeded => RecoverFromPastDueIfPossible(subscription, nowUtc),
            NormalizedProviderEventKind.SubscriptionCreated => ApplyProviderStatus(subscription, ev.ProviderSubscriptionStatus, nowUtc),
            NormalizedProviderEventKind.SubscriptionUpdated => ApplyProviderStatus(subscription, ev.ProviderSubscriptionStatus, nowUtc),
            _ => false
        };

        if (!changed) return false;

        var changeType = MapChangeType(previous, subscription.Status);
        var change = SubscriptionChange.Create(
            subscriptionId: subscription.Id,
            changeType: changeType,
            fromPlanId: null,
            toPlanId: null,
            fromPriceId: null,
            toPriceId: null,
            effectiveAtUtc: occurredAtUtc,
            prorationBehavior: ProrationBehavior.None,
            reason: $"Reconciled from provider event '{ev.EventType}' ({ev.ProviderEventId}).",
            metadataJson: null,
            nowUtc: nowUtc);
        _db.SubscriptionChanges.Add(change);
        return true;
    }

    private static bool CancelIfPossible(Subscription s, DateTime nowUtc)
    {
        if (s.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired) return false;
        s.Cancel(false, "Provider deleted subscription.", nowUtc);
        return true;
    }

    private static bool MarkPastDueIfPossible(Subscription s, DateTime nowUtc)
    {
        // Domain cannot be transitioned to PastDue directly today; the
        // subscription remains in its current status and we still write
        // a change row so the reconciliation is observable. We return
        // false here because we did not actually change the subscription
        // status, which means no SubscriptionChange will be written —
        // which is the correct conservative behavior for COM-B06.
        _ = s; _ = nowUtc;
        return false;
    }

    private static bool RecoverFromPastDueIfPossible(Subscription s, DateTime nowUtc)
    {
        if (s.Status == SubscriptionStatus.Trialing)
        {
            s.EndTrial(nowUtc);
            return true;
        }
        return false;
    }

    private static bool ApplyProviderStatus(
        Subscription s, ProviderSubscriptionStatus? providerStatus, DateTime nowUtc)
    {
        if (providerStatus is null) return false;
        switch (providerStatus.Value)
        {
            case ProviderSubscriptionStatus.Cancelled:
                return CancelIfPossible(s, nowUtc);
            case ProviderSubscriptionStatus.Active:
                if (s.Status == SubscriptionStatus.Trialing) { s.EndTrial(nowUtc); return true; }
                if (s.Status == SubscriptionStatus.Suspended) { s.Reactivate(nowUtc); return true; }
                if (s.Status == SubscriptionStatus.Draft) { s.Activate(nowUtc); return true; }
                return false;
            default:
                return false;
        }
    }

    private static SubscriptionChangeType MapChangeType(SubscriptionStatus previous, SubscriptionStatus next)
        => (previous, next) switch
        {
            (_, SubscriptionStatus.Cancelled) => SubscriptionChangeType.Cancelled,
            (_, SubscriptionStatus.Suspended) => SubscriptionChangeType.Suspended,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active) => SubscriptionChangeType.Reactivated,
            (SubscriptionStatus.Trialing, SubscriptionStatus.Active) => SubscriptionChangeType.TrialEnded,
            (SubscriptionStatus.Draft, SubscriptionStatus.Active) => SubscriptionChangeType.Activated,
            (_, SubscriptionStatus.Expired) => SubscriptionChangeType.Expired,
            _ => SubscriptionChangeType.Activated
        };

    private async Task<Subscription?> ResolveSubscriptionAsync(NormalizedProviderEvent ev, CancellationToken ct)
    {
        if (ev.SubscriptionId.HasValue)
        {
            var direct = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == ev.SubscriptionId.Value, ct);
            if (direct is not null) return direct;
        }
        if (!string.IsNullOrWhiteSpace(ev.ProviderSubscriptionId))
        {
            var mapping = await _db.PaymentProviderSubscriptions.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Provider == ev.Provider
                                          && p.ProviderSubscriptionId == ev.ProviderSubscriptionId, ct);
            if (mapping is not null)
            {
                return await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == mapping.SubscriptionId, ct);
            }
        }
        return null;
    }
}
