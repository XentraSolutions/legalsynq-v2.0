using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.AccountStanding;

/// <summary>
/// Singleton-per-billing-account derived snapshot of an account's
/// commercial standing. Updated only by the standing engine.
/// </summary>
public sealed class AccountStanding : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public AccountStandingStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTime? GracePeriodEndsAtUtc { get; private set; }
    public DateTime? PastDueSinceUtc { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime LastEvaluatedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private AccountStanding() { }

    public static AccountStanding Create(Guid billingAccountId, DateTime nowUtc)
    {
        if (billingAccountId == Guid.Empty)
            throw new InvalidOperationException("BillingAccountId is required.");
        return new AccountStanding
        {
            Id = Guid.NewGuid(),
            BillingAccountId = billingAccountId,
            Status = AccountStandingStatus.Good,
            LastEvaluatedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public bool Apply(
        AccountStandingStatus status,
        string? reason,
        DateTime? gracePeriodEndsAtUtc,
        DateTime? pastDueSinceUtc,
        DateTime? suspendedAtUtc,
        DateTime nowUtc)
    {
        var changed =
            Status != status
            || Reason != reason
            || GracePeriodEndsAtUtc != gracePeriodEndsAtUtc
            || PastDueSinceUtc != pastDueSinceUtc
            || SuspendedAtUtc != suspendedAtUtc;

        Status = status;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        GracePeriodEndsAtUtc = gracePeriodEndsAtUtc;
        PastDueSinceUtc = pastDueSinceUtc;
        SuspendedAtUtc = suspendedAtUtc;
        LastEvaluatedAtUtc = nowUtc;
        if (changed) UpdatedAtUtc = nowUtc;
        return changed;
    }
}
