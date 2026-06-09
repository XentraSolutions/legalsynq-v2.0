using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Payments.Services;

/// <summary>
/// Translates a payment-related <see cref="NormalizedProviderEvent"/> into
/// a Commerce-owned <see cref="Payment"/> + <see cref="PaymentAttempt"/>.
/// Idempotent on (Provider, ProviderEventId) via the
/// <c>ux_payment_attempts_provider_event_id</c> unique index plus an explicit
/// pre-check; idempotent on (Provider, ProviderPaymentId) via the
/// <c>ux_payments_provider_provider_payment_id</c> unique index.
/// </summary>
public sealed class PaymentRecordingService : IPaymentRecordingService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;

    public PaymentRecordingService(CommerceDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PaymentRecordingResult?> RecordFromEventAsync(
        NormalizedProviderEvent ev, bool succeeded, CancellationToken ct)
    {
        var billingAccountId = await ResolveBillingAccountIdAsync(ev, ct);
        if (billingAccountId == Guid.Empty) return null;

        // Idempotency: if we've already recorded an attempt for this
        // (provider, providerEventId), short-circuit.
        if (!string.IsNullOrWhiteSpace(ev.ProviderEventId))
        {
            var existingAttempt = await _db.PaymentAttempts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Provider == ev.Provider
                                          && a.ProviderEventId == ev.ProviderEventId, ct);
            if (existingAttempt is not null)
            {
                Payment? existingPayment = null;
                if (existingAttempt.PaymentId.HasValue)
                {
                    existingPayment = await _db.Payments.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == existingAttempt.PaymentId.Value, ct);
                }
                return new PaymentRecordingResult(
                    existingAttempt.PaymentId ?? Guid.Empty,
                    existingAttempt.Id,
                    existingPayment?.Status ?? (succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed),
                    existingPayment?.InvoiceId is not null);
            }
        }

        var subscriptionId = await ResolveSubscriptionIdAsync(ev, ct);

        // Locate or create the Payment.
        Payment? payment = null;
        if (!string.IsNullOrWhiteSpace(ev.ProviderPaymentIntentId))
        {
            payment = await _db.Payments.FirstOrDefaultAsync(
                p => p.Provider == ev.Provider
                     && p.ProviderPaymentId == ev.ProviderPaymentIntentId, ct);
        }

        var amount = ev.AmountMinor ?? 0;
        var currency = ev.Currency ?? await ResolveCurrencyAsync(billingAccountId, ct);

        if (payment is null)
        {
            payment = Payment.Create(
                billingAccountId,
                invoiceId: null,
                subscriptionId: subscriptionId,
                provider: ev.Provider,
                providerPaymentId: ev.ProviderPaymentIntentId,
                providerCustomerId: ev.ProviderCustomerId,
                amountMinor: amount,
                currency: currency,
                status: succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed,
                nowUtc: _clock.UtcNow);
            _db.Payments.Add(payment);
        }
        else
        {
            payment.UpdateAmount(amount, currency, _clock.UtcNow);
            if (succeeded) payment.MarkSucceeded(_clock.UtcNow);
            else payment.MarkFailed(ev.FailureCode, ev.FailureMessage, _clock.UtcNow);
        }

        // Best-effort invoice match by ProviderInvoiceId.
        Invoice? invoice = null;
        if (!string.IsNullOrWhiteSpace(ev.ProviderInvoiceId))
        {
            invoice = await _db.Invoices.FirstOrDefaultAsync(
                i => i.Provider == ev.Provider
                     && i.ProviderInvoiceId == ev.ProviderInvoiceId, ct);
            if (invoice is not null)
            {
                payment.AttachInvoice(invoice.Id, _clock.UtcNow);
                if (succeeded && invoice.Status is InvoiceStatus.Open or InvoiceStatus.Draft)
                {
                    invoice.RegisterPayment(amount, _clock.UtcNow);
                }
            }
        }

        var attemptStatus = succeeded
            ? PaymentAttemptStatus.Succeeded
            : PaymentAttemptStatus.Failed;
        var attempt = PaymentAttempt.Create(
            paymentId: payment.Id,
            billingAccountId: billingAccountId,
            subscriptionId: subscriptionId,
            provider: ev.Provider,
            providerEventId: ev.ProviderEventId,
            attemptedAtUtc: ev.OccurredAtUtc ?? _clock.UtcNow,
            status: attemptStatus,
            errorCode: succeeded ? null : ev.FailureCode,
            errorMessage: succeeded ? null : ev.FailureMessage,
            nowUtc: _clock.UtcNow);
        _db.PaymentAttempts.Add(attempt);

        return new PaymentRecordingResult(
            payment.Id, attempt.Id, payment.Status, invoice is not null);
    }

    private async Task<Guid> ResolveBillingAccountIdAsync(NormalizedProviderEvent ev, CancellationToken ct)
    {
        if (ev.BillingAccountId.HasValue) return ev.BillingAccountId.Value;
        if (!string.IsNullOrWhiteSpace(ev.ProviderCustomerId))
        {
            var customer = await _db.PaymentProviderCustomers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Provider == ev.Provider
                                          && c.ProviderCustomerId == ev.ProviderCustomerId, ct);
            if (customer is not null) return customer.BillingAccountId;
        }
        // Try via subscription mapping.
        if (!string.IsNullOrWhiteSpace(ev.ProviderSubscriptionId))
        {
            var mapping = await _db.PaymentProviderSubscriptions.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Provider == ev.Provider
                                          && p.ProviderSubscriptionId == ev.ProviderSubscriptionId, ct);
            if (mapping is not null)
            {
                var sub = await _db.Subscriptions.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == mapping.SubscriptionId, ct);
                if (sub is not null) return sub.BillingAccountId;
            }
        }
        return Guid.Empty;
    }

    private async Task<Guid?> ResolveSubscriptionIdAsync(NormalizedProviderEvent ev, CancellationToken ct)
    {
        if (ev.SubscriptionId.HasValue) return ev.SubscriptionId.Value;
        if (!string.IsNullOrWhiteSpace(ev.ProviderSubscriptionId))
        {
            var mapping = await _db.PaymentProviderSubscriptions.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Provider == ev.Provider
                                          && p.ProviderSubscriptionId == ev.ProviderSubscriptionId, ct);
            if (mapping is not null) return mapping.SubscriptionId;
        }
        return null;
    }

    private async Task<string> ResolveCurrencyAsync(Guid billingAccountId, CancellationToken ct)
    {
        var acct = await _db.BillingAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == billingAccountId, ct);
        return acct?.DefaultCurrency ?? "USD";
    }
}
