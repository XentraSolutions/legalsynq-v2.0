using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

public sealed class PaymentService : IPaymentService
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Lifecycle status assigned to payments persisted by this service on
    /// creation. The lifecycle is one-way (<see cref="RecordedStatus"/> →
    /// <see cref="VoidedStatus"/>); the transition is performed by
    /// <see cref="ReverseAsync"/> and is the only way to leave Recorded.
    /// </summary>
    public const string RecordedStatus = "Recorded";

    /// <summary>
    /// MS-BILL-WRITE-002 — terminal lifecycle status assigned by
    /// <see cref="ReverseAsync"/>. Voided payments are excluded from
    /// the invoice paid-sum aggregator
    /// (<c>SumRecordedPaymentsForInvoiceAsync</c>), so flipping a
    /// payment to Voided automatically lowers the parent invoice's
    /// paid total. The financial fields on the row are NOT mutated.
    /// </summary>
    public const string VoidedStatus = "Voided";

    /// <summary>
    /// MS-BILL-WRITE-002 — maximum length, in characters, of
    /// <see cref="Payment.ReversalReason"/>. Mirrors the column width set
    /// in <c>BillingDbContext</c> and the API DTO StringLength attribute.
    /// </summary>
    public const int MaxReversalReasonLength = 1000;

    /// <summary>
    /// Maximum length, in characters, of <see cref="Payment.Notes"/>. Mirrors
    /// the column width and the API DTO StringLength attribute so that a
    /// non-API caller (e.g. another service composing PaymentService directly)
    /// gets the same invariant enforcement as an HTTP client. The trim
    /// happens before this check.
    /// </summary>
    public const int MaxNotesLength = 2000;

    private readonly IPaymentRepository _repository;
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(
        IPaymentRepository repository,
        IInvoiceRepository invoices,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _invoices = invoices;
        _unitOfWork = unitOfWork;
    }

    public async Task<Payment> CreateAsync(
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        string currency,
        string method,
        string status,
        string? transactionReference,
        DateTime? paidAt,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));
        if (amount <= 0) throw new InvalidPaymentAmountException(amount);
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Method is required.", nameof(method));

        // Atomicity boundary: insert the payment + recompute invoice status in
        // a single DB transaction. If anything throws between the two writes,
        // disposing the transaction without committing rolls them both back so
        // we never end up with a payment row whose parent invoice's status is
        // stale.
        // Defense-in-depth notes length check. The API DTO already caps notes
        // at 2000 via StringLength, but a non-HTTP caller composing this
        // service directly would otherwise bypass that cap and only fail at
        // the EF column-width boundary with an opaque DbUpdateException. We
        // trim first so a caller padding with whitespace still gets the
        // intended length surface.
        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmedNotes is not null && trimmedNotes.Length > MaxNotesLength)
        {
            throw new ArgumentException(
                $"Notes must be at most {MaxNotesLength} characters.", nameof(notes));
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        // Acquire a row lock on the invoice before reading its current
        // payments. This serializes concurrent payment attempts on the same
        // invoice so two callers cannot both observe an old paid sum and
        // collectively overpay. The tenant-scoped overload prevents a caller
        // that knows a foreign tenant's invoice id from acquiring a lock on
        // that row at all — cross-tenant contention is impossible.
        await tx.LockInvoiceForUpdateAsync(tenantId, invoiceId, ct);

        // Tenant-scoped lookup ensures a payment can only attach to an invoice
        // owned by the calling tenant. Cross-tenant attempts surface as a
        // generic "not found" with no existence leak — same response as a
        // truly missing invoice.
        var invoice = await _invoices.GetByIdForTenantAsync(tenantId, invoiceId, ct)
            ?? throw new InvoiceNotFoundException(tenantId, invoiceId);

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));
        if (!string.Equals(normalizedCurrency, invoice.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new CurrencyMismatchException(normalizedCurrency, invoice.Currency);
        }

        // Lifecycle gate: only invoices in an active billable state can accept
        // payments. Draft must be Issued first; Voided/Refunded are terminal;
        // Paid would always be an overpayment and is rejected below.
        if (!InvoiceStatus.AcceptsPayments(invoice.Status))
        {
            throw new InvalidInvoicePaymentStateException(invoiceId, invoice.Status);
        }

        var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        // Read paid sum from the loaded aggregate (already filtered to only
        // non-voided payments by the lifecycle invariant — voiding sets the
        // invoice path to Refunded/etc separately). This is the same value
        // the SumRecordedPaymentsForInvoiceAsync repository method returns.
        var existingPaid = invoice.Payments.Where(p => p.Status != "Voided").Sum(p => p.Amount);
        var newPaidSum = existingPaid + roundedAmount;
        if (newPaidSum > invoice.TotalAmount)
        {
            var remaining = invoice.TotalAmount - existingPaid;
            throw new OverpaymentException(invoiceId, roundedAmount, remaining);
        }

        // Idempotency guard: if the caller supplies a TransactionReference
        // (e.g. a Stripe charge id from a webhook), refuse to record a second
        // payment with the same reference for this tenant. Without this check
        // a duplicate webhook delivery would be persisted as two payments and
        // could flip the invoice to Paid based on phantom money. The DB
        // unique index on (TenantId, TransactionReference) is the ultimate
        // guarantee; this pre-flight check just turns the common case into a
        // clean 409 instead of relying on a SaveChanges exception.
        var normalizedReference = string.IsNullOrWhiteSpace(transactionReference)
            ? null
            : transactionReference.Trim();
        if (normalizedReference is not null &&
            await _repository.ExistsByTenantAndReferenceAsync(tenantId, normalizedReference, ct))
        {
            throw new DuplicatePaymentReferenceException(tenantId, normalizedReference);
        }

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Amount = roundedAmount,
            Currency = normalizedCurrency,
            Method = method.Trim(),
            // Status is server-controlled. Callers (including the API DTO)
            // do not get to set arbitrary values; the lifecycle is just
            // Recorded → Voided. We keep the parameter so existing tests can
            // pass a label, but only RecordedStatus and a non-blank legacy
            // value are honored — anything else collapses to RecordedStatus.
            Status = string.IsNullOrWhiteSpace(status) ? RecordedStatus : status.Trim(),
            TransactionReference = normalizedReference,
            PaidAt = paidAt ?? now,
            CreatedAt = now,
            Notes = trimmedNotes,
        };

        var saved = await _repository.AddAsync(payment, ct);

        // Drive the parent invoice's status from the new payment total. This
        // keeps dashboards and dunning derived from a single source of truth:
        // the recorded payments.
        var newStatus = InvoiceStatus.ComputeStatus(
            invoice.Status, invoice.TotalAmount, newPaidSum, invoice.DueDate, now);
        if (newStatus != invoice.Status)
        {
            await _invoices.UpdateStatusAsync(tenantId, invoiceId, newStatus, now, null, ct);
        }

        await tx.CommitAsync(ct);
        return saved;
    }

    public Task<Payment?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (id == Guid.Empty) throw new ArgumentException("PaymentId is required.", nameof(id));
        return _repository.GetByIdForTenantAsync(tenantId, id, ct);
    }

    public Task<IReadOnlyList<Payment>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _repository.GetAllForTenantAsync(tenantId, ct);

    public async Task<PaymentPage> ListPagedAsync(
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        var clampedPage = page < 1 ? 1 : page;
        var clampedSize = pageSize <= 0 ? DefaultPageSize : (pageSize > MaxPageSize ? MaxPageSize : pageSize);

        var items = await _repository.ListAsync(tenantId, invoiceId, status, method, fromDate, toDate, clampedPage, clampedSize, ct);
        var total = await _repository.CountAsync(tenantId, invoiceId, status, method, fromDate, toDate, ct);
        return new PaymentPage(items, total);
    }

    public async Task<IReadOnlyList<Payment>?> GetByInvoiceAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        // Tenant-scoped existence check first so a missing/cross-tenant
        // invoice surfaces as a clean 404 instead of a misleading empty 200.
        var invoice = await _invoices.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        return await _repository.GetByInvoiceIdAsync(tenantId, invoiceId, ct);
    }

    public async Task<ReversePaymentResult> ReverseAsync(
        Guid tenantId,
        Guid paymentId,
        string reason,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (paymentId == Guid.Empty) throw new ArgumentException("PaymentId is required.", nameof(paymentId));

        // Defensive normalization & validation BEFORE we open a transaction
        // or hit the database — a blank/oversized reason is a 400 caller
        // error and should never tie up a row lock.
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
        if (trimmedReason.Length == 0)
        {
            throw new InvalidReversalReasonException(
                "Reversal reason is required.", MaxReversalReasonLength);
        }
        if (trimmedReason.Length > MaxReversalReasonLength)
        {
            throw new InvalidReversalReasonException(
                $"Reversal reason must be at most {MaxReversalReasonLength} characters.",
                MaxReversalReasonLength);
        }

        // Atomicity boundary: read the payment + lock its invoice + flip
        // status + (optionally) demote invoice status, all in one DB
        // transaction. If anything throws between the steps the dispose
        // rolls them back together, so we never end up with a Voided
        // payment whose parent invoice is still flagged Paid.
        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        // Pre-flight tenant-scoped existence check. We deliberately read
        // BEFORE acquiring the invoice lock so a cross-tenant probe does
        // not produce observable lock contention on another tenant's
        // invoice row.
        var existing = await _repository.GetByIdForTenantAsync(tenantId, paymentId, ct)
            ?? throw new PaymentNotFoundException(tenantId, paymentId);

        // Lifecycle gate. The Voided check is FIRST and reports a distinct
        // exception so the API can return 409 with a tailored message and
        // the BFF audit log can distinguish "duplicate reversal" from
        // "wrong-state reversal" (e.g. against a legacy Pending row).
        if (string.Equals(existing.Status, VoidedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentAlreadyReversedException(paymentId);
        }
        if (!string.Equals(existing.Status, RecordedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentNotReversibleException(paymentId, existing.Status);
        }

        // Acquire a tenant-scoped row lock on the parent invoice so a
        // concurrent CreateAsync against the same invoice cannot race the
        // status recomputation below. Using the tenant-scoped overload
        // means a caller that knows another tenant's invoice id (somehow)
        // still cannot acquire the lock — same defence as CreateAsync.
        // The invoice lock ALSO serializes concurrent reversal attempts
        // on the same payment, because every payment belongs to exactly
        // one invoice — so two simultaneous Reverse calls on the same
        // payment id queue on the same lock and execute one-at-a-time.
        await tx.LockInvoiceForUpdateAsync(tenantId, existing.InvoiceId, ct);

        // SEVERE-fix (architect): re-read the payment AFTER the lock and
        // re-run the lifecycle gate. The pre-lock check above is a fast
        // path so a cross-tenant probe / oversize reason / already-Voided
        // row is rejected without lock contention; but two concurrent
        // reverse callers can BOTH pass that check, then queue on the
        // invoice lock. Without this re-check the second caller would
        // overwrite the first one's `ReversedAt` / `ReversalReason`
        // audit fields and the duplicate would observably succeed (200)
        // instead of the required 409. The re-read here is CHEAP (a
        // primary-key lookup against a single row) compared to the
        // integrity damage of a clobbered audit trail.
        var locked = await _repository.GetByIdForTenantAsync(tenantId, paymentId, ct)
            ?? throw new PaymentNotFoundException(tenantId, paymentId);
        if (string.Equals(locked.Status, VoidedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentAlreadyReversedException(paymentId);
        }
        if (!string.Equals(locked.Status, RecordedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentNotReversibleException(paymentId, locked.Status);
        }
        // Use the post-lock snapshot for the rest of the method so we
        // never write stale data back. A concurrent edit between the
        // pre-lock read and the lock acquisition is now invisible to
        // the write path.
        existing = locked;

        // Re-load the parent invoice AFTER the lock so we observe the
        // committed state any concurrent transaction left behind.
        var invoice = await _invoices.GetByIdForTenantAsync(tenantId, existing.InvoiceId, ct)
            ?? throw new PaymentNotFoundException(tenantId, paymentId);

        var now = DateTime.UtcNow;

        // Append-only mutation: financial fields stay verbatim. Only the
        // lifecycle status flips and the two audit fields are populated.
        // We construct a fresh DTO-shaped Payment so the original loaded
        // row's tracking state cannot leak in (GetByIdForTenantAsync used
        // AsNoTracking()). UpdateAsync attaches the entity as Modified.
        var voided = new Payment
        {
            Id = existing.Id,
            TenantId = existing.TenantId,
            InvoiceId = existing.InvoiceId,
            Amount = existing.Amount,
            Currency = existing.Currency,
            Method = existing.Method,
            Status = VoidedStatus,
            TransactionReference = existing.TransactionReference,
            PaidAt = existing.PaidAt,
            CreatedAt = existing.CreatedAt,
            Notes = existing.Notes,
            ReversedAt = now,
            ReversalReason = trimmedReason,
        };

        var saved = await _repository.UpdateAsync(voided, ct);

        // Recompute the paid total from the durable source (the repository
        // aggregator), which already excludes Voided rows — so the total
        // we get back is the new, lower one. We do NOT subtract from the
        // loaded aggregate to avoid double-counting if the invoice load
        // happens to include the just-voided row.
        var newPaidSum = await _repository.SumRecordedPaymentsForInvoiceAsync(
            tenantId, existing.InvoiceId, ct);

        var newStatus = InvoiceStatus.ComputeStatus(
            invoice.Status, invoice.TotalAmount, newPaidSum, invoice.DueDate, now);
        if (newStatus != invoice.Status)
        {
            await _invoices.UpdateStatusAsync(tenantId, existing.InvoiceId, newStatus, now, null, ct);
        }

        await tx.CommitAsync(ct);

        var balanceDue = invoice.TotalAmount - newPaidSum;
        var summary = new InvoicePaymentSummary(
            invoice.Id,
            invoice.InvoiceNumber,
            newStatus,
            invoice.TotalAmount,
            newPaidSum,
            balanceDue,
            invoice.Currency);

        return new ReversePaymentResult(saved, summary);
    }

    public async Task<Payment> UpdateNotesAsync(
        Guid tenantId,
        Guid paymentId,
        string? notes,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (paymentId == Guid.Empty) throw new ArgumentException("PaymentId is required.", nameof(paymentId));

        // Normalise BEFORE we hit the database so a length-bound violation
        // is rejected without an unnecessary round-trip. Whitespace-only
        // input collapses to null (clear), matching the create-path
        // semantic in CreateAsync (see line 88 above).
        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes!.Trim();
        if (trimmedNotes is not null && trimmedNotes.Length > MaxNotesLength)
        {
            throw new InvalidPaymentNotesException(
                $"Notes must be at most {MaxNotesLength} characters.", MaxNotesLength);
        }

        // Tenant-scoped lookup. Cross-tenant probes surface through the same
        // PaymentNotFoundException as truly-missing ids — no existence
        // leak. We do NOT acquire an invoice lock here because notes have
        // no effect on balances, lifecycle status, or any aggregator —
        // SaveChangesAsync is atomic for a single row update and that's
        // the whole transactional boundary needed.
        var existing = await _repository.GetByIdForTenantAsync(tenantId, paymentId, ct)
            ?? throw new PaymentNotFoundException(tenantId, paymentId);

        // Append-only behaviour: copy ALL fields verbatim, mutate ONLY
        // Notes. The construction-not-mutation pattern matches ReverseAsync
        // so we never re-attach the AsNoTracking() loaded entity in a way
        // that could let stale tracking state leak in. UpdateAsync attaches
        // as Modified.
        var updated = new Payment
        {
            Id = existing.Id,
            TenantId = existing.TenantId,
            InvoiceId = existing.InvoiceId,
            Amount = existing.Amount,
            Currency = existing.Currency,
            Method = existing.Method,
            Status = existing.Status,
            TransactionReference = existing.TransactionReference,
            PaidAt = existing.PaidAt,
            CreatedAt = existing.CreatedAt,
            Notes = trimmedNotes,
            ReversedAt = existing.ReversedAt,
            ReversalReason = existing.ReversalReason,
        };

        return await _repository.UpdateAsync(updated, ct);
    }

    public async Task<InvoicePaymentSummary?> GetInvoicePaymentSummaryAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        var invoice = await _invoices.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // Source of truth for the paid total: the recorded (non-voided)
        // payments. We don't re-read from the loaded aggregate so the
        // summary stays consistent with what the future void flow would
        // exclude.
        var totalPaid = await _repository.SumRecordedPaymentsForInvoiceAsync(tenantId, invoiceId, ct);
        var balanceDue = invoice.TotalAmount - totalPaid;

        return new InvoicePaymentSummary(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status,
            invoice.TotalAmount,
            totalPaid,
            balanceDue,
            invoice.Currency);
    }
}
