using Commerce.Domain.Common;
using Commerce.Domain.Subscriptions.Enums;

namespace Commerce.Domain.Subscriptions;

public sealed class Subscription : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public string SubscriptionNumber { get; private set; } = default!;
    public SubscriptionStatus Status { get; private set; }
    public DateTime StartDateUtc { get; private set; }
    public DateTime CurrentPeriodStartUtc { get; private set; }
    public DateTime CurrentPeriodEndUtc { get; private set; }
    public DateTime? TrialStartUtc { get; private set; }
    public DateTime? TrialEndUtc { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Subscription() { }

    public static Subscription Create(
        Guid billingAccountId,
        string subscriptionNumber,
        DateTime startDateUtc,
        DateTime currentPeriodStartUtc,
        DateTime currentPeriodEndUtc,
        DateTime? trialStartUtc,
        DateTime? trialEndUtc,
        DateTime nowUtc)
    {
        if (currentPeriodEndUtc <= currentPeriodStartUtc)
            throw new InvalidOperationException("CurrentPeriodEndUtc must be after CurrentPeriodStartUtc.");
        if (trialStartUtc.HasValue ^ trialEndUtc.HasValue)
            throw new InvalidOperationException("TrialStartUtc and TrialEndUtc must be set together.");
        if (trialStartUtc.HasValue && trialEndUtc!.Value <= trialStartUtc.Value)
            throw new InvalidOperationException("TrialEndUtc must be after TrialStartUtc.");

        var status = trialStartUtc.HasValue ? SubscriptionStatus.Trialing : SubscriptionStatus.Active;

        return new Subscription
        {
            Id = Guid.NewGuid(),
            BillingAccountId = billingAccountId,
            SubscriptionNumber = subscriptionNumber.Trim(),
            Status = status,
            StartDateUtc = startDateUtc,
            CurrentPeriodStartUtc = currentPeriodStartUtc,
            CurrentPeriodEndUtc = currentPeriodEndUtc,
            TrialStartUtc = trialStartUtc,
            TrialEndUtc = trialEndUtc,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new InvalidOperationException("Cancelled subscription cannot be activated.");
        if (Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException("Expired subscription cannot be activated.");
        if (Status == SubscriptionStatus.Active) return;
        if (Status != SubscriptionStatus.Draft && Status != SubscriptionStatus.Trialing)
            throw new InvalidOperationException(
                $"Subscription in status '{Status}' cannot be activated; only Draft or Trialing may be activated.");

        Status = SubscriptionStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Suspend(DateTime nowUtc)
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new InvalidOperationException("Cancelled subscription cannot be suspended.");
        if (Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException("Expired subscription cannot be suspended.");
        if (Status != SubscriptionStatus.Active)
            throw new InvalidOperationException(
                $"Subscription in status '{Status}' cannot be suspended; only Active may be suspended in this block.");

        Status = SubscriptionStatus.Suspended;
        UpdatedAtUtc = nowUtc;
    }

    public void Reactivate(DateTime nowUtc)
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new InvalidOperationException("Cancelled subscription cannot be reactivated.");
        if (Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException("Expired subscription cannot be reactivated.");
        if (Status != SubscriptionStatus.Suspended)
            throw new InvalidOperationException(
                $"Subscription in status '{Status}' cannot be reactivated; only Suspended may be reactivated.");

        Status = SubscriptionStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Cancel(bool cancelAtPeriodEnd, string? reason, DateTime nowUtc)
    {
        if (Status == SubscriptionStatus.Cancelled || Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException(
                $"Subscription in status '{Status}' cannot be cancelled.");
        if (Status != SubscriptionStatus.Active
            && Status != SubscriptionStatus.Trialing
            && Status != SubscriptionStatus.Suspended
            && Status != SubscriptionStatus.PastDue)
            throw new InvalidOperationException(
                $"Subscription in status '{Status}' cannot be cancelled.");

        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (cancelAtPeriodEnd)
        {
            // Scheduled cancel: keep current status, raise the flag.
            CancelAtPeriodEnd = true;
        }
        else
        {
            Status = SubscriptionStatus.Cancelled;
            CancelledAtUtc = nowUtc;
            CancelAtPeriodEnd = false;
        }
        UpdatedAtUtc = nowUtc;
    }

    public void Renew(DateTime newPeriodStartUtc, DateTime newPeriodEndUtc, DateTime nowUtc)
    {
        if (Status != SubscriptionStatus.Active)
            throw new InvalidOperationException(
                $"Subscription in status '{Status}' cannot be renewed; only Active may be renewed.");
        if (newPeriodEndUtc <= newPeriodStartUtc)
            throw new InvalidOperationException("New CurrentPeriodEndUtc must be after CurrentPeriodStartUtc.");

        CurrentPeriodStartUtc = newPeriodStartUtc;
        CurrentPeriodEndUtc = newPeriodEndUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void EndTrial(DateTime nowUtc)
    {
        if (Status == SubscriptionStatus.Trialing)
        {
            Status = SubscriptionStatus.Active;
            UpdatedAtUtc = nowUtc;
        }
    }

    public void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;
}
