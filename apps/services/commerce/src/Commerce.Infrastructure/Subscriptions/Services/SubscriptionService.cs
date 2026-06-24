using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Application.Subscriptions.Abstractions;
using Commerce.Contracts.Subscriptions;
using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Subscriptions.Mapping;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Subscriptions.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    private const int SubscriptionNumberMaxAttempts = 5;

    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly ISubscriptionNumberGenerator _numbers;
    private readonly SubscriptionChangeWriter _history;
    private readonly IValidator<CreateSubscriptionRequest> _createValidator;
    private readonly IValidator<ChangeSubscriptionPlanRequest> _changePlanValidator;
    private readonly IValidator<CancelSubscriptionRequest> _cancelValidator;
    private readonly IValidator<RenewSubscriptionRequest> _renewValidator;
    // TB-INT-03 — optional auto-publish queue. Tests construct the
    // service directly without it (null-publisher = no enqueue).
    private readonly ITenantBillingEntitlementPublishQueue? _publishQueue;
    // TB-INT-04 — optional durable outbox. When OutboxEnabled is
    // true the trigger sites prefer the outbox over the in-memory
    // queue; when null (legacy unit tests) the helper falls back to
    // the in-memory queue path or no-op.
    private readonly ITenantBillingEntitlementOutbox? _publishOutbox;
    private readonly IOptions<Commerce.Infrastructure.Integration.TenantBilling.TenantBillingClientOptions>? _publishOptions;
    private readonly Commerce.Infrastructure.Integration.TenantBilling.TenantBillingPublisherMetrics? _publishMetrics;
    private readonly ILogger<SubscriptionService> _log;

    public SubscriptionService(
        CommerceDbContext db,
        IClock clock,
        ISubscriptionNumberGenerator numbers,
        SubscriptionChangeWriter history,
        IValidator<CreateSubscriptionRequest> createValidator,
        IValidator<ChangeSubscriptionPlanRequest> changePlanValidator,
        IValidator<CancelSubscriptionRequest> cancelValidator,
        IValidator<RenewSubscriptionRequest> renewValidator,
        ITenantBillingEntitlementPublishQueue? publishQueue = null,
        Commerce.Infrastructure.Integration.TenantBilling.TenantBillingPublisherMetrics? publishMetrics = null,
        ILogger<SubscriptionService>? log = null,
        ITenantBillingEntitlementOutbox? publishOutbox = null,
        IOptions<Commerce.Infrastructure.Integration.TenantBilling.TenantBillingClientOptions>? publishOptions = null)
    {
        _db = db;
        _clock = clock;
        _numbers = numbers;
        _history = history;
        _createValidator = createValidator;
        _changePlanValidator = changePlanValidator;
        _cancelValidator = cancelValidator;
        _renewValidator = renewValidator;
        _publishQueue = publishQueue;
        _publishOutbox = publishOutbox;
        _publishOptions = publishOptions;
        _publishMetrics = publishMetrics;
        _log = log ?? NullLogger<SubscriptionService>.Instance;
    }

    /// <summary>
    /// TB-INT-03 — best-effort post-commit enqueue. Never throws and
    /// never re-raises into the caller; a failure here must not roll
    /// back the just-committed Commerce transaction.
    /// </summary>
    /// <summary>
    /// Routes the auto-publish trigger to either the durable outbox
    /// (TB-INT-04, preferred when <c>OutboxEnabled</c> is true) or
    /// the in-process queue (TB-INT-03 fallback). Always best-effort.
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
                    _log.LogDebug(
                        "Auto-publish enqueue skipped (disabled): BA={BillingAccountId} Trigger={TriggerSource}",
                        billingAccountId, triggerSource);
                    break;
                case EnqueueResult.DroppedQueueFull:
                    _publishMetrics?.RecordAutoPublishDropped(triggerSource, "queue-full");
                    _log.LogWarning(
                        "Auto-publish enqueue dropped (queue full): BA={BillingAccountId} Trigger={TriggerSource} Capacity={Capacity}",
                        billingAccountId, triggerSource, _publishQueue.Capacity);
                    break;
                case EnqueueResult.Invalid:
                    _publishMetrics?.RecordAutoPublishDropped(triggerSource, "invalid");
                    _log.LogWarning(
                        "Auto-publish enqueue rejected (invalid input): BA={BillingAccountId} Trigger={TriggerSource}",
                        billingAccountId, triggerSource);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Never let an enqueue path throw out — Commerce already
            // committed and must not be rolled back on auxiliary
            // bookkeeping failure.
            _log.LogError(ex,
                "Auto-publish enqueue threw unexpectedly: BA={BillingAccountId} Trigger={TriggerSource}",
                billingAccountId, triggerSource);
        }
    }

    // ---------------------------------------------------------------- Create

    public async Task<SubscriptionResponse> CreateAsync(CreateSubscriptionRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        var account = await _db.BillingAccounts.FindAsync(new object[] { request.BillingAccountId }, ct)
            ?? throw new NotFoundException("BillingAccount", request.BillingAccountId.ToString());
        if (account.Status == BillingAccountStatus.Closed)
            throw new InvalidStateTransitionException("Cannot create subscription on a closed billing account.");
        if (account.Status == BillingAccountStatus.Suspended)
            throw new InvalidStateTransitionException("Cannot create subscription on a suspended billing account.");

        var plan = await _db.Plans.FindAsync(new object[] { request.PlanId }, ct)
            ?? throw new NotFoundException("Plan", request.PlanId.ToString());
        if (plan.Status != CatalogStatus.Active)
            throw new InvalidRelationshipException("Plan must be Active to create a subscription.");

        var price = await _db.Prices.FindAsync(new object[] { request.PriceId }, ct)
            ?? throw new NotFoundException("Price", request.PriceId.ToString());
        if (price.Status != CatalogStatus.Active)
            throw new InvalidRelationshipException("Price must be Active to create a subscription.");
        if (price.PlanId != request.PlanId)
            throw new InvalidRelationshipException("Price does not belong to the specified Plan.");

        var now = _clock.UtcNow;
        var startDate = request.StartDateUtc ?? now;

        DateTime? trialStart = null;
        DateTime? trialEnd = null;
        var effectiveTrialDays = request.TrialDays ?? plan.TrialDays;
        if (effectiveTrialDays is > 0)
        {
            trialStart = startDate;
            trialEnd = startDate.AddDays(effectiveTrialDays.Value);
        }

        var periodStart = trialEnd ?? startDate;
        var periodEnd = BillingPeriodCalculator.NextPeriodEnd(periodStart, price.BillingInterval);

        for (var attempt = 1; attempt <= SubscriptionNumberMaxAttempts; attempt++)
        {
            var number = await _numbers.AllocateAsync(ct);
            Subscription subscription;
            try
            {
                subscription = Subscription.Create(
                    request.BillingAccountId, number, startDate,
                    periodStart, periodEnd, trialStart, trialEnd, now);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidStateTransitionException(ex.Message);
            }

            var item = SubscriptionItem.Create(
                subscription.Id, plan.Id, price.Id, request.Quantity,
                price.AmountMinor, price.Currency, price.BillingInterval,
                periodStart, now);

            _db.Subscriptions.Add(subscription);
            _db.SubscriptionItems.Add(item);

            _history.Append(subscription.Id, SubscriptionChangeType.Created, now,
                toPlanId: plan.Id, toPriceId: price.Id,
                metadataJson: request.MetadataJson);

            if (trialStart.HasValue)
            {
                _history.Append(subscription.Id, SubscriptionChangeType.TrialStarted, trialStart.Value);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                await TryEnqueueAutoPublishAsync(subscription.BillingAccountId, "subscription-created", CancellationToken.None);
                return await BuildResponseAsync(subscription.Id, ct);
            }
            catch (DbUpdateException)
            {
                ResetTracker();
                if (attempt >= SubscriptionNumberMaxAttempts)
                    throw new DuplicateKeyException("Subscription", "SubscriptionNumber");
            }
        }

        throw new DuplicateKeyException("Subscription", "SubscriptionNumber");
    }

    private void ResetTracker()
    {
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
        }
    }

    // ---------------------------------------------------------------- Reads

    public async Task<IReadOnlyList<SubscriptionResponse>> ListAsync(Guid? billingAccountId, CancellationToken ct)
    {
        var q = _db.Subscriptions.AsNoTracking().AsQueryable();
        if (billingAccountId.HasValue) q = q.Where(s => s.BillingAccountId == billingAccountId.Value);
        var subs = await q.OrderBy(s => s.SubscriptionNumber).ToListAsync(ct);
        if (subs.Count == 0) return Array.Empty<SubscriptionResponse>();

        var ids = subs.Select(s => s.Id).ToList();
        var items = await _db.SubscriptionItems.AsNoTracking()
            .Where(i => ids.Contains(i.SubscriptionId))
            .ToListAsync(ct);
        var grouped = items.GroupBy(i => i.SubscriptionId).ToDictionary(g => g.Key, g => g.ToList());
        return subs.Select(s => s.ToResponse(grouped.TryGetValue(s.Id, out var its) ? its : new List<SubscriptionItem>())).ToList();
    }

    public async Task<SubscriptionResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var sub = await _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Subscription", id.ToString());
        var items = await _db.SubscriptionItems.AsNoTracking()
            .Where(i => i.SubscriptionId == id)
            .ToListAsync(ct);
        return sub.ToResponse(items);
    }

    public async Task<IReadOnlyList<SubscriptionChangeResponse>> ListChangesAsync(Guid id, CancellationToken ct)
    {
        if (!await _db.Subscriptions.AsNoTracking().AnyAsync(s => s.Id == id, ct))
            throw new NotFoundException("Subscription", id.ToString());
        var changes = await _db.SubscriptionChanges.AsNoTracking()
            .Where(c => c.SubscriptionId == id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        return changes.Select(SubscriptionMappers.ToResponse).ToList();
    }

    // ---------------------------------------------------------------- Lifecycle

    public Task<SubscriptionResponse> ActivateAsync(Guid id, CancellationToken ct)
        => MutateAsync(id, (sub, now) =>
        {
            sub.Activate(now);
            _history.Append(sub.Id, SubscriptionChangeType.Activated, now);
        }, ct, triggerSource: "subscription-activated");

    public Task<SubscriptionResponse> SuspendAsync(Guid id, CancellationToken ct)
        => MutateAsync(id, (sub, now) =>
        {
            sub.Suspend(now);
            _history.Append(sub.Id, SubscriptionChangeType.Suspended, now);
        }, ct, triggerSource: "subscription-suspended");

    public Task<SubscriptionResponse> ReactivateAsync(Guid id, CancellationToken ct)
        => MutateAsync(id, (sub, now) =>
        {
            sub.Reactivate(now);
            _history.Append(sub.Id, SubscriptionChangeType.Reactivated, now);
        }, ct, triggerSource: "subscription-reactivated");

    public async Task<SubscriptionResponse> CancelAsync(Guid id, CancelSubscriptionRequest request, CancellationToken ct)
    {
        await _cancelValidator.ValidateAndThrowAsync(request, ct);

        return await MutateAsync(id, (sub, now) =>
        {
            sub.Cancel(request.CancelAtPeriodEnd, request.Reason, now);

            if (!request.CancelAtPeriodEnd)
            {
                // Immediate cancel: close all currently active items now.
                var items = _db.SubscriptionItems.Local
                    .Where(i => i.SubscriptionId == sub.Id && i.Status == SubscriptionItemStatus.Active)
                    .ToList();
                foreach (var item in items) item.CancelImmediate(now);
            }

            _history.Append(sub.Id, SubscriptionChangeType.Cancelled, now, reason: request.Reason);
        }, ct, loadItems: true, triggerSource: "subscription-cancelled");
    }

    public async Task<SubscriptionResponse> RenewAsync(Guid id, RenewSubscriptionRequest? request, CancellationToken ct)
    {
        request ??= new RenewSubscriptionRequest();
        await _renewValidator.ValidateAndThrowAsync(request, ct);

        // Renew is intentionally NOT wired to auto-publish (TB-INT-03):
        // a renewal advances the billing period but does not change
        // the entitlement set or access recommendation. Re-publishing
        // on every renewal would amplify load on Tenant Billing
        // without changing what gets stored. If account standing
        // moves as a result of an unpaid renewal it will trigger
        // through AccountStandingService.EvaluateAsync separately.
        return await MutateAsync(id, (sub, now) =>
        {
            // Pick the dominant interval from active items. If multiple
            // intervals exist (uncommon in this block), Monthly wins.
            var items = _db.SubscriptionItems.Local
                .Where(i => i.SubscriptionId == sub.Id && i.Status == SubscriptionItemStatus.Active)
                .ToList();
            if (items.Count == 0)
                throw new InvalidStateTransitionException("Subscription has no active items to renew.");

            var interval = items.Select(i => i.BillingInterval)
                .OrderBy(bi => (int)bi)
                .First();

            var newStart = request.NewPeriodStartUtc ?? sub.CurrentPeriodEndUtc;
            var newEnd = BillingPeriodCalculator.NextPeriodEnd(newStart, interval);

            try { sub.Renew(newStart, newEnd, now); }
            catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }

            _history.Append(sub.Id, SubscriptionChangeType.Renewed, now);
        }, ct, loadItems: true);
    }

    public async Task<SubscriptionResponse> ChangePlanAsync(Guid id, ChangeSubscriptionPlanRequest request, CancellationToken ct)
    {
        await _changePlanValidator.ValidateAndThrowAsync(request, ct);

        var sub = await _db.Subscriptions.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Subscription", id.ToString());

        if (sub.Status != SubscriptionStatus.Active && sub.Status != SubscriptionStatus.Trialing)
            throw new InvalidStateTransitionException(
                $"Subscription in status '{sub.Status}' cannot change plan; only Active or Trialing subscriptions may change plan.");

        var newPlan = await _db.Plans.FindAsync(new object[] { request.NewPlanId }, ct)
            ?? throw new NotFoundException("Plan", request.NewPlanId.ToString());
        if (newPlan.Status != CatalogStatus.Active)
            throw new InvalidRelationshipException("New plan must be Active.");

        var newPrice = await _db.Prices.FindAsync(new object[] { request.NewPriceId }, ct)
            ?? throw new NotFoundException("Price", request.NewPriceId.ToString());
        if (newPrice.Status != CatalogStatus.Active)
            throw new InvalidRelationshipException("New price must be Active.");
        if (newPrice.PlanId != request.NewPlanId)
            throw new InvalidRelationshipException("New price does not belong to the specified plan.");

        var now = _clock.UtcNow;
        var effective = request.EffectiveAtUtc ?? now;

        var existingItems = await _db.SubscriptionItems
            .Where(i => i.SubscriptionId == sub.Id && i.Status == SubscriptionItemStatus.Active)
            .ToListAsync(ct);

        // For COM-B04 we only support a single-line plan change. If the
        // subscription has multiple active items we keep them and only
        // close those tied to the prior plan/price pair.
        Guid? fromPlanId = existingItems.Select(i => (Guid?)i.PlanId).FirstOrDefault();
        Guid? fromPriceId = existingItems.Select(i => (Guid?)i.PriceId).FirstOrDefault();

        foreach (var item in existingItems)
        {
            // Guard the close-time vs effective-from constraint.
            var closeAt = effective <= item.EffectiveFromUtc ? item.EffectiveFromUtc.AddTicks(1) : effective;
            try { item.Close(closeAt, now); }
            catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }
        }

        var quantity = request.Quantity ?? Math.Max(1, existingItems.Sum(i => i.Quantity));
        var newItem = SubscriptionItem.Create(
            sub.Id, newPlan.Id, newPrice.Id, quantity,
            newPrice.AmountMinor, newPrice.Currency, newPrice.BillingInterval,
            effective, now);
        _db.SubscriptionItems.Add(newItem);

        sub.Touch(now);

        _history.Append(sub.Id, SubscriptionChangeType.PlanChanged, effective,
            fromPlanId: fromPlanId, toPlanId: newPlan.Id,
            fromPriceId: fromPriceId, toPriceId: newPrice.Id,
            prorationBehavior: request.ProrationBehavior,
            reason: request.Reason, metadataJson: request.MetadataJson);

        await _db.SaveChangesAsync(ct);
        await TryEnqueueAutoPublishAsync(sub.BillingAccountId, "subscription-plan-changed", CancellationToken.None);
        return await BuildResponseAsync(sub.Id, ct);
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<SubscriptionResponse> MutateAsync(
        Guid id,
        Action<Subscription, DateTime> apply,
        CancellationToken ct,
        bool loadItems = false,
        string? triggerSource = null)
    {
        var sub = await _db.Subscriptions.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Subscription", id.ToString());

        if (loadItems)
        {
            // Bring active items into the change tracker so the apply
            // delegate can mutate them through DbContext.Local.
            await _db.SubscriptionItems
                .Where(i => i.SubscriptionId == id)
                .LoadAsync(ct);
        }

        var now = _clock.UtcNow;
        try { apply(sub, now); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }

        await _db.SaveChangesAsync(ct);
        if (triggerSource is not null)
        {
            await TryEnqueueAutoPublishAsync(sub.BillingAccountId, triggerSource, CancellationToken.None);
        }
        return await BuildResponseAsync(id, ct);
    }

    private async Task<SubscriptionResponse> BuildResponseAsync(Guid id, CancellationToken ct)
    {
        var sub = await _db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == id, ct);
        var items = await _db.SubscriptionItems.AsNoTracking()
            .Where(i => i.SubscriptionId == id)
            .ToListAsync(ct);
        return sub.ToResponse(items);
    }
}
