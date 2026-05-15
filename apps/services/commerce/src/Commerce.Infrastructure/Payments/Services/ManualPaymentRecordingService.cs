using System.Data;
using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments;
using Commerce.Infrastructure.Payments.Mapping;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Payments.Services;

/// <summary>
/// Records an admin-entered "manual" payment against an invoice. Unlike
/// <see cref="PaymentRecordingService"/> (which is webhook-driven and
/// idempotent on provider event ids), this service is interactive and
/// rejects rather than de-duplicates: each call produces exactly one
/// new <see cref="Payment"/> row with provider <c>Manual</c> and status
/// <c>Succeeded</c>, applied to the invoice via
/// <c>Invoice.RegisterPayment</c> in the same unit of work.
/// </summary>
public sealed class ManualPaymentRecordingService : IManualPaymentRecordingService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<RecordManualPaymentRequest> _validator;

    public ManualPaymentRecordingService(
        CommerceDbContext db,
        IClock clock,
        IValidator<RecordManualPaymentRequest> validator)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
    }

    public async Task<PaymentResponse> RecordAsync(
        Guid invoiceId,
        RecordManualPaymentRequest request,
        CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        // Concurrency guard: two admins (or a quick double-click) hitting
        // this endpoint against the same open invoice could both pass the
        // overpayment check on stale reads and then race to update
        // invoice.AmountPaidMinor — last writer wins, totals diverge from
        // the payments ledger.
        //
        // Scope note: this fully serializes manual-vs-manual writes on a
        // single invoice. It does NOT serialize against the webhook-driven
        // PaymentRecordingService, which still mutates Invoice without a
        // row lock (its SaveChangesAsync is owned by PaymentWebhookService,
        // so a lock acquired here would release before the save). A
        // cross-path race is therefore still possible if a Stripe webhook
        // arrives concurrently with a manual entry. Tracking that as a
        // follow-up: either thread the transaction down to the webhook
        // orchestrator, or introduce an optimistic concurrency token on
        // Invoice so both writers retry on conflict.
        //
        // Resolution strategy:
        //  * On a relational DB we open a Serializable transaction and
        //    pull the invoice with `SELECT ... FOR UPDATE`, which acquires
        //    an exclusive row lock for the duration of the transaction.
        //    The second concurrent caller will block until we commit and
        //    then re-read the updated balance, at which point its own
        //    overpayment / already-paid checks fire correctly.
        //  * On the EF InMemory provider (used in unit tests) transactions
        //    and FromSqlRaw aren't supported, so we fall back to a plain
        //    tracked read. Tests run single-threaded so this is safe; the
        //    relational path is what protects production.
        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            var response = await RecordCoreAsync(invoiceId, request, lockRow: true, ct);
            await tx.CommitAsync(ct);
            return response;
        }

        return await RecordCoreAsync(invoiceId, request, lockRow: false, ct);
    }

    private async Task<PaymentResponse> RecordCoreAsync(
        Guid invoiceId,
        RecordManualPaymentRequest request,
        bool lockRow,
        CancellationToken ct)
    {
        Invoice? invoice;
        if (lockRow)
        {
            // FromSqlInterpolated parameterizes safely; the FOR UPDATE
            // clause holds a row lock until tx.CommitAsync completes.
            invoice = await _db.Invoices
                .FromSqlInterpolated(
                    $"SELECT * FROM invoices WHERE id = {invoiceId} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
        }
        else
        {
            invoice = await _db.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        }

        if (invoice is null)
            throw new NotFoundException("Invoice", invoiceId.ToString());

        // Domain rules: refuse void / paid invoices with a precise 4xx
        // message rather than letting Invoice.RegisterPayment surface a
        // generic exception or silently no-op. The exception types map
        // through ProblemDetailsExceptionMiddleware: state transitions
        // → 409, relationship/balance violations → 422.
        if (invoice.Status == InvoiceStatus.Void)
            throw new InvalidStateTransitionException(
                $"Cannot record a payment against void invoice {invoice.Id}.");
        if (invoice.Status == InvoiceStatus.Paid || invoice.AmountDueMinor == 0)
            throw new InvalidStateTransitionException(
                $"Invoice {invoice.Id} is already fully paid; nothing further to record.");

        if (request.AmountMinor > invoice.AmountDueMinor)
            throw new InvalidRelationshipException(
                $"Payment amount ({request.AmountMinor}) exceeds the invoice balance due ({invoice.AmountDueMinor}).");

        var nowUtc = _clock.UtcNow;

        // Persist the Payment row first so we can attribute it to the
        // invoice; then call RegisterPayment to mutate invoice totals.
        // Both writes are committed in a single SaveChangesAsync below.
        var payment = Payment.CreateManual(
            billingAccountId: invoice.BillingAccountId,
            invoiceId: invoice.Id,
            subscriptionId: invoice.SubscriptionId,
            amountMinor: request.AmountMinor,
            currency: invoice.Currency,
            paidAtUtc: request.PaidAtUtc,
            method: request.Method,
            transactionReference: request.TransactionReference,
            recordedByLabel: request.RecordedByLabel,
            notes: request.Notes,
            nowUtc: nowUtc);

        _db.Payments.Add(payment);
        invoice.RegisterPayment(request.AmountMinor, nowUtc);

        await _db.SaveChangesAsync(ct);

        return payment.ToResponse();
    }
}
