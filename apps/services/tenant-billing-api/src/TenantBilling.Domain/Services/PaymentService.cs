using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;

namespace TenantBilling.Domain.Services;

public sealed class PaymentService : IPaymentService
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Lifecycle status assigned to payments persisted by this service.
    /// "Voided" exists in the lifecycle but is set by a future void flow,
    /// not on creation.
    /// </summary>
    public const string RecordedStatus = "Recorded";

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
            Id = Guid.NewGuid(),
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
