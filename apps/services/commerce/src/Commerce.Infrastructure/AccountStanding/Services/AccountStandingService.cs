using Commerce.Application.AccountStanding.Abstractions;
using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.AccountStanding;
using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Integration.TenantBilling;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using AccountStandingEntity = Commerce.Domain.AccountStanding.AccountStanding;
using AccountStandingPolicyValue = Commerce.Domain.AccountStanding.AccountStandingPolicy;

namespace Commerce.Infrastructure.AccountStanding.Services;

/// <summary>
/// Account-standing engine. Computes a fresh
/// <see cref="AccountStandingStatus"/> from the current state of the
/// billing account, its subscriptions, and its open invoices, then
/// upserts the singleton <see cref="AccountStanding"/> row for the
/// billing account.
/// </summary>
public sealed class AccountStandingService : IAccountStandingService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly AccountStandingPolicyValue _policy;
    // TB-INT-03 — optional auto-publish queue. Tests can construct
    // the service without it.
    private readonly ITenantBillingEntitlementPublishQueue? _publishQueue;
    private readonly ITenantBillingEntitlementOutbox? _publishOutbox;
    private readonly IOptions<TenantBillingClientOptions>? _publishOptions;
    private readonly TenantBillingPublisherMetrics? _publishMetrics;
    private readonly ILogger<AccountStandingService> _log;

    public AccountStandingService(
        CommerceDbContext db,
        IClock clock,
        AccountStandingPolicyValue policy,
        ITenantBillingEntitlementPublishQueue? publishQueue = null,
        TenantBillingPublisherMetrics? publishMetrics = null,
        ILogger<AccountStandingService>? log = null,
        ITenantBillingEntitlementOutbox? publishOutbox = null,
        IOptions<TenantBillingClientOptions>? publishOptions = null)
    {
        _db = db;
        _clock = clock;
        _policy = policy;
        _publishQueue = publishQueue;
        _publishOutbox = publishOutbox;
        _publishOptions = publishOptions;
        _publishMetrics = publishMetrics;
        _log = log ?? NullLogger<AccountStandingService>.Instance;
    }

    public async Task<AccountStandingResponse> EvaluateAsync(Guid billingAccountId, CancellationToken ct)
    {
        var account = await _db.BillingAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == billingAccountId, ct)
            ?? throw new NotFoundException("BillingAccount", billingAccountId.ToString());

        var subs = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.BillingAccountId == billingAccountId)
            .ToListAsync(ct);

        var openInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.BillingAccountId == billingAccountId
                        && i.Status == InvoiceStatus.Open)
            .ToListAsync(ct);

        var nowUtc = _clock.UtcNow;
        var (status, reason, gracePeriodEndsAt, pastDueSince, suspendedAt) =
            Evaluate(account.Status, subs, openInvoices, nowUtc, _policy);

        var standing = await _db.AccountStandings
            .FirstOrDefaultAsync(a => a.BillingAccountId == billingAccountId, ct);
        if (standing is null)
        {
            standing = AccountStandingEntity.Create(billingAccountId, nowUtc);
            _db.AccountStandings.Add(standing);
        }
        standing.Apply(status, reason, gracePeriodEndsAt, pastDueSince, suspendedAt, nowUtc);
        await _db.SaveChangesAsync(ct);

        // TB-INT-03 — best-effort post-commit auto-publish. Account
        // standing is the single most important driver of the
        // entitlement snapshot's AccessRecommendation; publishing on
        // every recalculation keeps Tenant Billing in sync without
        // requiring the host to call the manual publish endpoint.
        await TryEnqueueAutoPublishAsync(billingAccountId, "account-standing-recalculated", CancellationToken.None);

        return ToResponse(standing);
    }

    /// <summary>
    /// TB-INT-04 — routes to the durable outbox when
    /// <c>OutboxEnabled</c> is true; otherwise falls back to the
    /// TB-INT-03 in-process queue. Always best-effort.
    /// </summary>
    private async Task TryEnqueueAutoPublishAsync(
        Guid billingAccountId, string triggerSource, CancellationToken ct)
    {
        var opts = _publishOptions?.Value.Normalised();
        if (opts is { OutboxEnabled: true } && _publishOutbox is not null)
        {
            try
            {
                var id = await _publishOutbox
                    .EnqueueAsync(billingAccountId, triggerSource, correlationId: null, ct)
                    .ConfigureAwait(false);
                if (id == Guid.Empty)
                {
                    _log.LogWarning(
                        "Outbox enqueue returned empty id (best-effort): BA={BillingAccountId} Trigger={TriggerSource}",
                        billingAccountId, triggerSource);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Outbox enqueue threw unexpectedly (swallowed; commit kept): BA={BillingAccountId} Trigger={TriggerSource}",
                    billingAccountId, triggerSource);
            }
            return;
        }

        TryEnqueueAutoPublish(billingAccountId, triggerSource);
    }

    private void TryEnqueueAutoPublish(Guid billingAccountId, string triggerSource)
    {
        if (_publishQueue is null) return;
        try
        {
            var item = new TenantBillingEntitlementPublishWorkItem(
                billingAccountId, triggerSource, _clock.UtcNow, CorrelationId: null);
            var result = _publishQueue.Enqueue(item);
            switch (result)
            {
                case EnqueueResult.Accepted:
                    _publishMetrics?.RecordAutoPublishEnqueued(triggerSource);
                    _log.LogDebug(
                        "Auto-publish enqueue accepted: BA={BillingAccountId} Trigger={TriggerSource} QueueDepth={QueueDepth}",
                        billingAccountId, triggerSource, _publishQueue.Depth);
                    break;
                case EnqueueResult.SkippedDisabled:
                    _publishMetrics?.RecordAutoPublishDropped(triggerSource, "auto-publish-disabled");
                    break;
                case EnqueueResult.DroppedQueueFull:
                    _publishMetrics?.RecordAutoPublishDropped(triggerSource, "queue-full");
                    _log.LogWarning(
                        "Auto-publish enqueue dropped (queue full): BA={BillingAccountId} Trigger={TriggerSource} Capacity={Capacity}",
                        billingAccountId, triggerSource, _publishQueue.Capacity);
                    break;
                case EnqueueResult.Invalid:
                    _publishMetrics?.RecordAutoPublishDropped(triggerSource, "invalid");
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Auto-publish enqueue threw unexpectedly: BA={BillingAccountId} Trigger={TriggerSource}",
                billingAccountId, triggerSource);
        }
    }

    public async Task<AccountStandingResponse> GetAsync(Guid billingAccountId, CancellationToken ct)
    {
        // Verify the billing account exists so callers see a 404 rather
        // than the more confusing "AccountStanding not found" when the
        // account itself is missing.
        var accountExists = await _db.BillingAccounts.AsNoTracking()
            .AnyAsync(a => a.Id == billingAccountId, ct);
        if (!accountExists)
            throw new NotFoundException("BillingAccount", billingAccountId.ToString());

        var standing = await _db.AccountStandings.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BillingAccountId == billingAccountId, ct)
            ?? throw new NotFoundException("AccountStanding", billingAccountId.ToString());
        return ToResponse(standing);
    }

    /// <summary>
    /// Pure evaluation function. Exposed internally so tests can drive
    /// the engine without the database.
    /// </summary>
    internal static (AccountStandingStatus Status,
                     string? Reason,
                     DateTime? GracePeriodEndsAt,
                     DateTime? PastDueSince,
                     DateTime? SuspendedAt)
        Evaluate(
            BillingAccountStatus accountStatus,
            IReadOnlyList<Commerce.Domain.Subscriptions.Subscription> subscriptions,
            IReadOnlyList<Commerce.Domain.Invoicing.Invoice> openInvoices,
            DateTime nowUtc,
            AccountStandingPolicyValue policy)
    {
        if (accountStatus == BillingAccountStatus.Closed)
            return (AccountStandingStatus.Closed, "BillingAccount is closed.", null, null, null);

        // Trialing wins over Good only when there are no overdue invoices.
        var pastDueInvoices = openInvoices
            .Where(i => i.AmountDueMinor > 0 && i.DueDateUtc.HasValue && i.DueDateUtc.Value < nowUtc)
            .OrderBy(i => i.DueDateUtc!.Value)
            .ToList();

        if (pastDueInvoices.Count > 0)
        {
            var earliest = pastDueInvoices[0].DueDateUtc!.Value;
            var graceEnd = earliest.AddDays(policy.GracePeriodDays);
            var suspendThreshold = earliest.AddDays(policy.PastDueToSuspendedDays);

            if (nowUtc >= suspendThreshold || accountStatus == BillingAccountStatus.Suspended)
                return (AccountStandingStatus.Suspended,
                    $"Account suspended after {policy.PastDueToSuspendedDays} days past due.",
                    graceEnd, earliest, nowUtc);

            if (nowUtc < graceEnd)
                return (AccountStandingStatus.GracePeriod,
                    $"Invoice past due since {earliest:O}; grace period until {graceEnd:O}.",
                    graceEnd, earliest, null);

            return (AccountStandingStatus.PastDue,
                $"Invoice past due since {earliest:O}; grace period ended.",
                graceEnd, earliest, null);
        }

        if (accountStatus == BillingAccountStatus.Suspended)
            return (AccountStandingStatus.Suspended, "BillingAccount is suspended.", null, null, nowUtc);

        // No open past-due invoices.
        var hasActive = subscriptions.Any(s =>
            s.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing);
        var allCancelled = subscriptions.Count > 0 &&
                           subscriptions.All(s => s.Status is SubscriptionStatus.Cancelled
                                                          or SubscriptionStatus.Expired);

        if (allCancelled)
            return (AccountStandingStatus.Cancelled, "All subscriptions are cancelled or expired.", null, null, null);

        if (subscriptions.Any(s => s.Status == SubscriptionStatus.Trialing))
            return (AccountStandingStatus.Trialing, "Subscription is trialing.", null, null, null);

        if (subscriptions.Any(s => s.Status == SubscriptionStatus.PastDue))
            return (AccountStandingStatus.PastDue, "A subscription is past due.", null, nowUtc, null);

        if (subscriptions.Any(s => s.Status == SubscriptionStatus.Suspended))
            return (AccountStandingStatus.Suspended, "A subscription is suspended.", null, null, nowUtc);

        if (hasActive || subscriptions.Count == 0)
            return (AccountStandingStatus.Good, null, null, null, null);

        return (AccountStandingStatus.Good, null, null, null, null);
    }

    private static AccountStandingResponse ToResponse(AccountStandingEntity s) => new(
        s.Id, s.BillingAccountId, s.Status, s.Reason,
        s.GracePeriodEndsAtUtc, s.PastDueSinceUtc, s.SuspendedAtUtc,
        s.LastEvaluatedAtUtc, s.CreatedAtUtc, s.UpdatedAtUtc);
}
