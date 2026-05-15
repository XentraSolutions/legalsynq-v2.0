namespace Commerce.Infrastructure.Integration.TenantBilling.Outbox;

/// <summary>
/// TB-INT-04 — durable outbox row for a single Commerce → Tenant
/// Billing entitlement publish work item. Owned by the Commerce
/// service; written by lifecycle trigger sites (subscription /
/// account-standing) and processed by
/// <see cref="TenantBillingEntitlementOutboxWorker"/>.
///
/// <para>One row per trigger event. Duplicates are intentionally
/// allowed — idempotency is the responsibility of Tenant Billing's
/// snapshot upsert. <see cref="Status"/> transitions:
/// <c>Pending → Processing → Published | Failed → Pending |
/// Abandoned</c>. <c>Published</c> and <c>Abandoned</c> are
/// terminal.</para>
/// </summary>
public sealed class TenantBillingEntitlementPublishOutboxRow
{
    public Guid Id { get; private set; }
    public Guid BillingAccountId { get; private set; }
    public string TriggerSource { get; private set; } = string.Empty;

    public TenantBillingEntitlementPublishOutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }

    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public string? LastOutcome { get; private set; }
    public string? LastReason { get; private set; }
    public int? LastHttpStatus { get; private set; }
    public string? LastErrorSummary { get; private set; }
    public string? CorrelationId { get; private set; }

    public DateTime? LockedAtUtc { get; private set; }
    public Guid? LockId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private TenantBillingEntitlementPublishOutboxRow() { }

    public static TenantBillingEntitlementPublishOutboxRow Create(
        Guid billingAccountId,
        string triggerSource,
        string? correlationId,
        int maxAttempts,
        DateTime nowUtc)
    {
        if (billingAccountId == Guid.Empty)
            throw new ArgumentException("billingAccountId required.", nameof(billingAccountId));
        if (string.IsNullOrWhiteSpace(triggerSource))
            throw new ArgumentException("triggerSource required.", nameof(triggerSource));
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        return new TenantBillingEntitlementPublishOutboxRow
        {
            Id = Guid.NewGuid(),
            BillingAccountId = billingAccountId,
            TriggerSource = triggerSource,
            Status = TenantBillingEntitlementPublishOutboxStatus.Pending,
            Attempts = 0,
            MaxAttempts = maxAttempts,
            NextAttemptAtUtc = nowUtc,
            CorrelationId = correlationId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    /// <summary>
    /// Mark the row as in-flight. Caller must atomically update via
    /// EF (or a SELECT FOR UPDATE on a relational provider) to
    /// prevent two workers from claiming the same row.
    /// </summary>
    public void MarkProcessing(Guid lockId, DateTime nowUtc)
    {
        Status = TenantBillingEntitlementPublishOutboxStatus.Processing;
        LockId = lockId;
        LockedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkPublished(string reason, int? httpStatus, DateTime nowUtc)
    {
        Status = TenantBillingEntitlementPublishOutboxStatus.Published;
        Attempts += 1;
        LastAttemptAtUtc = nowUtc;
        PublishedAtUtc = nowUtc;
        LastOutcome = "published";
        LastReason = reason;
        LastHttpStatus = httpStatus;
        LastErrorSummary = null;
        LockId = null;
        LockedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkAbandoned(string outcome, string reason, int? httpStatus, string? errorSummary, DateTime nowUtc)
    {
        Status = TenantBillingEntitlementPublishOutboxStatus.Abandoned;
        Attempts += 1;
        LastAttemptAtUtc = nowUtc;
        LastOutcome = outcome;
        LastReason = reason;
        LastHttpStatus = httpStatus;
        LastErrorSummary = errorSummary;
        LockId = null;
        LockedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFailedAndScheduleRetry(
        string reason,
        int? httpStatus,
        string? errorSummary,
        DateTime nextAttemptAtUtc,
        DateTime nowUtc)
    {
        Status = TenantBillingEntitlementPublishOutboxStatus.Pending;
        Attempts += 1;
        LastAttemptAtUtc = nowUtc;
        LastOutcome = "failed";
        LastReason = reason;
        LastHttpStatus = httpStatus;
        LastErrorSummary = errorSummary;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LockId = null;
        LockedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Skipped result that should be re-tried later (publisher
    /// disabled, transient infra-side gating). Does NOT count an
    /// attempt because no real wire call was made.
    /// </summary>
    public void RescheduleSkipped(string reason, DateTime nextAttemptAtUtc, DateTime nowUtc)
    {
        Status = TenantBillingEntitlementPublishOutboxStatus.Pending;
        LastAttemptAtUtc = nowUtc;
        LastOutcome = "skipped";
        LastReason = reason;
        LastHttpStatus = null;
        LastErrorSummary = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LockId = null;
        LockedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Recovery path for rows seen in <c>Processing</c> with a stale
    /// lock — return them to <c>Pending</c> so the next poll picks
    /// them up. Does not count an attempt.
    /// </summary>
    public void RecoverStaleProcessing(DateTime nowUtc)
    {
        Status = TenantBillingEntitlementPublishOutboxStatus.Pending;
        LockId = null;
        LockedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }
}

public enum TenantBillingEntitlementPublishOutboxStatus
{
    Pending = 1,
    Processing = 2,
    Published = 3,
    Failed = 4,
    Abandoned = 5,
}
