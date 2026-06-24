using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Payments.Mapping;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Payments.Services;

public sealed class PaymentWebhookService : IPaymentWebhookService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IPaymentProviderRegistry _registry;
    private readonly IPaymentRecordingService _recording;
    private readonly ISubscriptionReconciliationService _reconciliation;

    public PaymentWebhookService(
        CommerceDbContext db,
        IClock clock,
        IPaymentProviderRegistry registry,
        IPaymentRecordingService recording,
        ISubscriptionReconciliationService reconciliation)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _recording = recording;
        _reconciliation = reconciliation;
    }

    public async Task<WebhookProcessingResult> ReceiveAsync(
        PaymentProviderType provider,
        string rawBody,
        string signatureHeader,
        CancellationToken ct)
    {
        var providerImpl = _registry.Get(provider);
        if (!providerImpl.IsEnabled)
            throw new PaymentProviderDisabledException(provider.ToString());

        // Step 1 — verify signature. Throws on failure (mapped to 400).
        providerImpl.VerifyWebhook(new ProviderWebhookPayload(rawBody, signatureHeader));

        // Step 2 — translate to normalized event.
        NormalizedProviderEvent normalized;
        try
        {
            normalized = providerImpl.TranslateWebhookEvent(rawBody);
        }
        catch (Exception ex)
        {
            // Record the bad payload; return Failed.
            var malformed = PaymentProviderEventLog.Receive(
                provider, $"unparsable-{Guid.CreateVersion7():N}", "unknown", rawBody, _clock.UtcNow);
            malformed.MarkFailed("Failed to parse webhook payload: " + ex.GetType().Name, _clock.UtcNow);
            _db.PaymentProviderEventLogs.Add(malformed);
            await _db.SaveChangesAsync(ct);
            return new WebhookProcessingResult(
                malformed.Id, PaymentProviderEventProcessingStatus.Failed, malformed.ErrorMessage);
        }

        if (string.IsNullOrWhiteSpace(normalized.ProviderEventId))
        {
            var bad = PaymentProviderEventLog.Receive(
                provider, $"missing-id-{Guid.CreateVersion7():N}", normalized.EventType, rawBody, _clock.UtcNow);
            bad.MarkFailed("Webhook payload is missing 'id'.", _clock.UtcNow);
            _db.PaymentProviderEventLogs.Add(bad);
            await _db.SaveChangesAsync(ct);
            return new WebhookProcessingResult(
                bad.Id, PaymentProviderEventProcessingStatus.Failed, bad.ErrorMessage);
        }

        // Step 3 — idempotency: detect duplicate by (Provider, ProviderEventId).
        var existing = await _db.PaymentProviderEventLogs.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Provider == provider && e.ProviderEventId == normalized.ProviderEventId, ct);
        if (existing is not null)
        {
            return new WebhookProcessingResult(
                existing.Id, PaymentProviderEventProcessingStatus.Duplicate, "duplicate");
        }

        // Step 4 — persist log row in 'Received' state.
        var log = PaymentProviderEventLog.Receive(
            provider, normalized.ProviderEventId, normalized.EventType, rawBody, _clock.UtcNow);
        _db.PaymentProviderEventLogs.Add(log);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent insert won the race; treat as duplicate.
            _db.Entry(log).State = EntityState.Detached;
            var winning = await _db.PaymentProviderEventLogs.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Provider == provider && e.ProviderEventId == normalized.ProviderEventId, ct);
            return new WebhookProcessingResult(
                winning?.Id ?? Guid.Empty,
                PaymentProviderEventProcessingStatus.Duplicate,
                "duplicate");
        }

        // Step 5 — apply normalized state changes.
        try
        {
            var status = await ApplyAsync(normalized, ct);
            if (status == PaymentProviderEventProcessingStatus.Ignored)
                log.MarkIgnored("Unsupported event type.", _clock.UtcNow);
            else
                log.MarkProcessed(_clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            return new WebhookProcessingResult(log.Id, log.ProcessingStatus, null);
        }
        catch (Exception ex)
        {
            log.MarkFailed(ex.GetType().Name + ": " + ex.Message, _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            return new WebhookProcessingResult(log.Id, log.ProcessingStatus, log.ErrorMessage);
        }
    }

    /// <summary>
    /// Apply mutations safe in COM-B05: update provider mappings and
    /// payment-method references. Commerce subscription lifecycle is
    /// NOT changed here; that belongs to a later block.
    /// </summary>
    private async Task<PaymentProviderEventProcessingStatus> ApplyAsync(
        NormalizedProviderEvent ev, CancellationToken ct)
    {
        switch (ev.Kind)
        {
            case NormalizedProviderEventKind.CheckoutSessionCompleted:
            {
                var mapping = await FindMappingAsync(ev, ct);
                if (mapping is null) return PaymentProviderEventProcessingStatus.Ignored;
                mapping.MarkActive(ev.ProviderSubscriptionId, _clock.UtcNow);
                return PaymentProviderEventProcessingStatus.Processed;
            }
            case NormalizedProviderEventKind.CheckoutSessionExpired:
            {
                var mapping = await FindMappingAsync(ev, ct);
                if (mapping is null) return PaymentProviderEventProcessingStatus.Ignored;
                mapping.MarkFailed(_clock.UtcNow);
                return PaymentProviderEventProcessingStatus.Processed;
            }
            case NormalizedProviderEventKind.SubscriptionCreated:
            case NormalizedProviderEventKind.SubscriptionUpdated:
            {
                var mapping = await FindMappingAsync(ev, ct);
                var reconciled = await _reconciliation.ReconcileFromEventAsync(ev, ct);
                if (mapping is null && !reconciled) return PaymentProviderEventProcessingStatus.Ignored;
                mapping?.MarkActive(ev.ProviderSubscriptionId, _clock.UtcNow);
                return PaymentProviderEventProcessingStatus.Processed;
            }
            case NormalizedProviderEventKind.SubscriptionDeleted:
            {
                var mapping = await FindMappingAsync(ev, ct);
                var reconciled = await _reconciliation.ReconcileFromEventAsync(ev, ct);
                if (mapping is null && !reconciled) return PaymentProviderEventProcessingStatus.Ignored;
                mapping?.MarkCancelled(_clock.UtcNow);
                return PaymentProviderEventProcessingStatus.Processed;
            }
            case NormalizedProviderEventKind.PaymentIntentSucceeded:
            case NormalizedProviderEventKind.InvoicePaymentSucceeded:
            {
                var recorded = await _recording.RecordFromEventAsync(ev, succeeded: true, ct);
                var reconciled = await _reconciliation.ReconcileFromEventAsync(ev, ct);
                if (recorded is null && !reconciled) return PaymentProviderEventProcessingStatus.Ignored;
                return PaymentProviderEventProcessingStatus.Processed;
            }
            case NormalizedProviderEventKind.PaymentIntentFailed:
            case NormalizedProviderEventKind.InvoicePaymentFailed:
            {
                var recorded = await _recording.RecordFromEventAsync(ev, succeeded: false, ct);
                var reconciled = await _reconciliation.ReconcileFromEventAsync(ev, ct);
                if (recorded is null && !reconciled) return PaymentProviderEventProcessingStatus.Ignored;
                return PaymentProviderEventProcessingStatus.Processed;
            }
            case NormalizedProviderEventKind.PaymentMethodAttached:
            {
                if (string.IsNullOrWhiteSpace(ev.ProviderPaymentMethodId)
                    || string.IsNullOrWhiteSpace(ev.ProviderCustomerId))
                    return PaymentProviderEventProcessingStatus.Ignored;

                // Locate the BillingAccount via the customer mapping.
                var customer = await _db.PaymentProviderCustomers
                    .FirstOrDefaultAsync(c => c.Provider == ev.Provider
                                              && c.ProviderCustomerId == ev.ProviderCustomerId, ct);
                if (customer is null) return PaymentProviderEventProcessingStatus.Ignored;

                var existing = await _db.PaymentMethodReferences
                    .FirstOrDefaultAsync(p => p.Provider == ev.Provider
                                              && p.ProviderPaymentMethodId == ev.ProviderPaymentMethodId, ct);
                if (existing is null)
                {
                    var pm = PaymentMethodReference.Create(
                        customer.BillingAccountId, ev.Provider, ev.ProviderPaymentMethodId!,
                        ev.ProviderCustomerId, ev.PaymentMethodBrand, ev.PaymentMethodLast4,
                        ev.PaymentMethodExpMonth, ev.PaymentMethodExpYear, _clock.UtcNow);
                    _db.PaymentMethodReferences.Add(pm);
                }
                else
                {
                    existing.UpdateFromProvider(
                        ev.ProviderCustomerId, ev.PaymentMethodBrand, ev.PaymentMethodLast4,
                        ev.PaymentMethodExpMonth, ev.PaymentMethodExpYear, _clock.UtcNow);
                }
                return PaymentProviderEventProcessingStatus.Processed;
            }
            default:
                return PaymentProviderEventProcessingStatus.Ignored;
        }
    }

    private async Task<PaymentProviderSubscription?> FindMappingAsync(
        NormalizedProviderEvent ev, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(ev.ProviderCheckoutSessionId))
        {
            var bySession = await _db.PaymentProviderSubscriptions.FirstOrDefaultAsync(
                p => p.Provider == ev.Provider
                     && p.ProviderCheckoutSessionId == ev.ProviderCheckoutSessionId, ct);
            if (bySession is not null) return bySession;
        }
        if (!string.IsNullOrWhiteSpace(ev.ProviderSubscriptionId))
        {
            var bySub = await _db.PaymentProviderSubscriptions.FirstOrDefaultAsync(
                p => p.Provider == ev.Provider
                     && p.ProviderSubscriptionId == ev.ProviderSubscriptionId, ct);
            if (bySub is not null) return bySub;
        }
        if (ev.SubscriptionId.HasValue)
        {
            var byId = await _db.PaymentProviderSubscriptions.FirstOrDefaultAsync(
                p => p.Provider == ev.Provider && p.SubscriptionId == ev.SubscriptionId.Value, ct);
            if (byId is not null) return byId;
        }
        return null;
    }

    public async Task<IReadOnlyList<PaymentProviderEventLogResponse>> ListAsync(
        PaymentProviderType? provider,
        PaymentProviderEventProcessingStatus? status,
        int take,
        CancellationToken ct)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;
        var q = _db.PaymentProviderEventLogs.AsNoTracking().AsQueryable();
        if (provider.HasValue) q = q.Where(e => e.Provider == provider.Value);
        if (status.HasValue) q = q.Where(e => e.ProcessingStatus == status.Value);
        var rows = await q.OrderByDescending(e => e.CreatedAtUtc).Take(take).ToListAsync(ct);
        return rows.Select(r => r.ToResponse()).ToList();
    }

    public async Task<PaymentProviderEventLogResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.PaymentProviderEventLogs.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("PaymentProviderEventLog", id.ToString());
        return row.ToResponse();
    }
}
