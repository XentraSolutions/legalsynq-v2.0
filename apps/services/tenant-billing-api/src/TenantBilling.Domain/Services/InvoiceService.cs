using System.Globalization;
using System.Text.RegularExpressions;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;

namespace TenantBilling.Domain.Services;

public sealed class InvoiceService : IInvoiceService
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    private readonly IInvoiceRepository _repository;
    private readonly ICustomerRepository _customers;
    private readonly IRefundRepository _refunds;
    private readonly InvoiceLifecycleService _lifecycle;
    private readonly IInvoiceTemplateSelectionService _templateSelection;
    private readonly IInvoiceTemplateStampingService _templateStamping;

    // Captures the trailing 6+ digit sequence in a generated INV-YYYY-NNNNNN
    // number. Used to derive the next sequence number for the tenant/year.
    private static readonly Regex InvoiceNumberSuffix =
        new(@"^INV-\d{4}-(\d{6,})$", RegexOptions.Compiled);

    public InvoiceService(
        IInvoiceRepository repository,
        ICustomerRepository customers,
        IRefundRepository refunds,
        InvoiceLifecycleService lifecycle,
        IInvoiceTemplateSelectionService templateSelection,
        IInvoiceTemplateStampingService templateStamping)
    {
        _repository = repository;
        _customers = customers;
        _refunds = refunds;
        _lifecycle = lifecycle;
        _templateSelection = templateSelection;
        _templateStamping = templateStamping;
    }

    public async Task<Invoice> CreateAsync(
        Guid tenantId,
        Guid customerId,
        string? invoiceNumber,
        DateTime issueDate,
        DateTime dueDate,
        string currency,
        string? notes,
        IReadOnlyList<NewInvoiceLine> lines,
        decimal taxAmount,
        decimal discountAmount = 0m,
        InvoiceTemplate? template = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.", nameof(currency));
        if (lines is null || lines.Count == 0) throw new ArgumentException("At least one line item is required.", nameof(lines));
        if (taxAmount < 0) throw new ArgumentException("TaxAmount must be >= 0.", nameof(taxAmount));
        if (discountAmount < 0) throw new ArgumentException("DiscountAmount must be >= 0.", nameof(discountAmount));
        if (dueDate < issueDate) throw new ArgumentException("DueDate must be on or after IssueDate.", nameof(dueDate));

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        foreach (var l in lines)
        {
            if (l.Quantity < 1) throw new ArgumentException("Line Quantity must be >= 1.", nameof(lines));
            if (l.UnitPrice < 0) throw new ArgumentException("Line UnitPrice must be >= 0.", nameof(lines));
            if (string.IsNullOrWhiteSpace(l.Description)) throw new ArgumentException("Line Description is required.", nameof(lines));
        }

        // Tenant-scoped + active-only lookup. A null result means the customer
        // doesn't exist, has been soft-deleted, or belongs to another tenant —
        // all of which are invalid for invoicing. The single tenant-scoped
        // query enforces ownership AND soft-delete in one shot, with no
        // cross-tenant existence leak (same response as a missing id).
        _ = await _customers.GetActiveByIdAsync(tenantId, customerId, ct)
            ?? throw new InvalidOperationException(
                $"Customer {customerId} does not belong to tenant {tenantId} or has been deleted.");

        // Resolve the invoice number BEFORE validating money so the duplicate-
        // number conflict (409) wins consistently over money-shape (400).
        var resolvedInvoiceNumber = await ResolveInvoiceNumberAsync(tenantId, invoiceNumber, issueDate, ct);

        var now = DateTime.UtcNow;
        var invoiceId = Guid.CreateVersion7();

        var lineItems = lines.Select(l => new InvoiceLineItem
        {
            Id = Guid.CreateVersion7(),
            InvoiceId = invoiceId,
            Description = l.Description.Trim(),
            Quantity = l.Quantity,
            UnitPrice = decimal.Round(l.UnitPrice, 2, MidpointRounding.AwayFromZero),
            LineTotal = decimal.Round(l.UnitPrice * l.Quantity, 2, MidpointRounding.AwayFromZero),
            CreatedAt = now,
        }).ToList();

        var subtotal = decimal.Round(lineItems.Sum(li => li.LineTotal), 2, MidpointRounding.AwayFromZero);
        var roundedTax = decimal.Round(taxAmount, 2, MidpointRounding.AwayFromZero);
        var roundedDiscount = decimal.Round(discountAmount, 2, MidpointRounding.AwayFromZero);

        // Discount cannot exceed gross (subtotal + tax) — otherwise the total
        // would go negative, which is meaningless for an invoice.
        if (roundedDiscount > subtotal + roundedTax)
            throw new ArgumentException(
                $"DiscountAmount {roundedDiscount} cannot exceed Subtotal+Tax ({subtotal + roundedTax}).",
                nameof(discountAmount));

        var invoice = new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            CustomerId = customerId,
            InvoiceNumber = resolvedInvoiceNumber,
            IssueDate = issueDate,
            DueDate = dueDate,
            Status = InvoiceStatus.Draft,
            Subtotal = subtotal,
            TaxAmount = roundedTax,
            DiscountAmount = roundedDiscount,
            TotalAmount = decimal.Round(subtotal + roundedTax - roundedDiscount, 2, MidpointRounding.AwayFromZero),
            Currency = normalizedCurrency,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            IssuedAt = null,
            LineItems = lineItems,
        };

        // INV-TPL-02: stamp the branding snapshot in-memory BEFORE
        // the repository insert so the entire invoice (snapshot
        // included) lands as a single atomic write. The controller
        // is responsible for resolving the effective template via
        // IInvoiceTemplateSelectionService — by the time we get here
        // a non-null template has already been validated as in-scope
        // and Active, so we stamp unconditionally. A null template
        // means the caller resolved through the chain and got
        // nothing (no explicit id, no tenant default) — that path
        // is supported and the invoice persists with no snapshot.
        if (template is not null)
        {
            _templateStamping.StampInvoice(invoice, template, now);
        }

        return await _repository.AddAsync(invoice, ct);
    }

    /// <summary>
    /// Decide the final invoice number to persist. Manual input is trimmed
    /// and uniqueness-checked; blank input is auto-generated by incrementing
    /// the latest <c>INV-YYYY-NNNNNN</c> sequence for this tenant + year. The
    /// year used is the IssueDate's UTC year so back-dated invoices land in
    /// the correct sequence.
    /// </summary>
    private async Task<string> ResolveInvoiceNumberAsync(
        Guid tenantId,
        string? invoiceNumber,
        DateTime issueDate,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var trimmed = invoiceNumber.Trim();
            if (await _repository.ExistsByTenantAndNumberAsync(tenantId, trimmed, null, ct))
                throw new DuplicateInvoiceNumberException(tenantId, trimmed);
            return trimmed;
        }

        var year = issueDate.Year;
        var latest = await _repository.GetLatestInvoiceNumberAsync(tenantId, year, ct);
        var nextSeq = 1;
        if (!string.IsNullOrEmpty(latest))
        {
            var match = InvoiceNumberSuffix.Match(latest);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var current))
                nextSeq = current + 1;
        }

        // In the very unlikely event the next slot is taken (e.g. a
        // concurrent request grabbed it, or a manually-supplied number
        // collides), walk forward until we find a free one. Bounded so a
        // pathological sequence can't loop forever.
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var candidate = $"INV-{year:D4}-{(nextSeq + attempt):D6}";
            if (!await _repository.ExistsByTenantAndNumberAsync(tenantId, candidate, null, ct))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Could not allocate an invoice number for tenant {tenantId} in year {year}.");
    }

    public Task<Invoice?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (id == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(id));
        return _repository.GetByIdForTenantAsync(tenantId, id, ct);
    }

    public Task<IReadOnlyList<Invoice>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _repository.GetAllForTenantAsync(tenantId, ct);

    public async Task<InvoicePage> ListPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        var clampedPage = page < 1 ? 1 : page;
        var clampedSize = pageSize <= 0 ? DefaultPageSize : (pageSize > MaxPageSize ? MaxPageSize : pageSize);

        var items = await _repository.ListAsync(tenantId, search, status, customerId, fromDate, toDate, clampedPage, clampedSize, ct);
        var total = await _repository.CountAsync(tenantId, search, status, customerId, fromDate, toDate, ct);
        return new InvoicePage(items, total);
    }

    public async Task<Invoice?> IssueAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        var invoice = await _repository.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // Single source of truth for what's a legal Draft → Issued edge.
        // The engine throws InvalidInvoiceTransitionException (which derives
        // InvalidOperationException) so the controller maps it to 400 and
        // the existing test suite that asserts InvalidOperationException
        // keeps passing.
        _lifecycle.ValidateTransition(invoice.Status, InvoiceStatus.Issued);

        var now = DateTime.UtcNow;

        // INV-TPL-02: ensure-stamp on the issue path. An invoice
        // that was created BEFORE its tenant configured a default
        // template (or that was created without an explicit
        // template) gets stamped here on its way to Issued — the
        // tenant's *current* default at issue-time becomes the
        // immutable snapshot. We deliberately do NOT re-stamp an
        // already-snapshotted invoice; the idempotency guard lives
        // in the stamping service so the snapshot taken at create
        // time always wins. We perform the stamp BEFORE the status
        // flip so each repo write keeps a single responsibility
        // (UpdateStatusAsync stays focused on lifecycle).
        if (invoice.InvoiceTemplateId is null)
        {
            var defaultTemplate = await _templateSelection
                .GetDefaultForTenantAsync(tenantId, ct);
            if (defaultTemplate is not null)
            {
                await _repository.ApplyStampAsync(
                    tenantId, invoiceId, defaultTemplate, now, ct);
            }
        }

        return await _repository.UpdateStatusAsync(tenantId, invoiceId, InvoiceStatus.Issued, now, now, ct);
    }

    public async Task<Invoice?> VoidAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        var invoice = await _repository.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // Structural gate: is current → Voided in the allowed-transition
        // graph? Rejects Paid → Voided, Refunded → Voided, etc.
        _lifecycle.ValidateTransition(invoice.Status, InvoiceStatus.Voided);

        // Operational guard on top of the structural gate: PartiallyPaid →
        // Voided IS in the transition graph (the spec lists it), but TBS-B05
        // explicitly chooses to block voiding any invoice with recorded
        // payments because we don't have a refund/adjustment workflow that
        // reconciles the cash impact. Operators must refund first, then
        // void from a Paid → PartiallyRefunded → ... path.
        if (invoice.Payments.Any())
        {
            throw new InvalidInvoiceStateException(
                invoiceId, invoice.Status,
                $"invoice has {invoice.Payments.Count} recorded payment(s); refund the payments before voiding.");
        }

        return await _repository.UpdateStatusAsync(tenantId, invoiceId, InvoiceStatus.Voided, DateTime.UtcNow, null, ct);
    }

    public async Task<Invoice?> ReevaluateAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        var invoice = await _repository.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        var paidSum = invoice.Payments.Sum(p => p.Amount);
        var newStatus = InvoiceStatus.ComputeStatus(
            invoice.Status, invoice.TotalAmount, paidSum, invoice.DueDate, DateTime.UtcNow);

        if (newStatus == invoice.Status) return invoice;

        // Belt-and-braces: ComputeStatus is the source of truth here, but
        // we still validate the resulting edge is in the lifecycle graph so
        // a future ComputeStatus tweak cannot smuggle in an illegal
        // transition (e.g. Paid → Issued). Validation throws
        // InvalidInvoiceTransitionException on a bad edge, which surfaces
        // at the API as a 400 — that's the correct signal for "the recompute
        // wants to do something we shouldn't permit".
        _lifecycle.ValidateTransition(invoice.Status, newStatus);
        return await _repository.UpdateStatusAsync(tenantId, invoiceId, newStatus, DateTime.UtcNow, null, ct);
    }

    public async Task<Invoice?> MarkOverdueAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        // Pre-read for diagnostic error messaging (so the API can return
        // 400 with a clear "already Paid" / "due in future" reason rather
        // than a generic 404). The actual transition is performed below
        // via TryMarkOverdueAsync, which is a race-safe conditional
        // update on relational providers.
        var invoice = await _repository.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // Structural gate: only Issued / PartiallyPaid can transition to
        // Overdue per the lifecycle graph. Draft / Paid / Voided / Refunded
        // / PartiallyRefunded are rejected here with a typed exception.
        _lifecycle.ValidateTransition(invoice.Status, InvoiceStatus.Overdue);

        // Operational gate: the due date must actually have passed. We
        // compare on date boundaries to mirror ComputeStatus's "due today
        // is not yet overdue" rule.
        var now = DateTime.UtcNow;
        if (now.Date <= invoice.DueDate.Date)
        {
            throw new InvalidInvoiceStateException(
                invoiceId, invoice.Status,
                $"due date {invoice.DueDate:O} has not passed at {now:O}.");
        }

        // Atomic CAS: writes Status=Overdue ONLY if the invoice still
        // matches the eligibility predicate at write time (relational
        // providers do this in a single SQL UPDATE; non-relational test
        // providers re-check the predicate before SaveChanges). This
        // closes the lost-update window where a concurrent payment moved
        // the invoice to Paid between our pre-read and our write.
        var updated = await _repository.TryMarkOverdueAsync(tenantId, invoiceId, now, ct);
        if (updated is not null) return updated;

        // CAS missed. Re-read to surface the most accurate diagnostic.
        // Treat a vanished row as 404 (matches the pre-read contract);
        // a row whose status no longer fits the eligibility predicate
        // (e.g. concurrent payment landed and flipped it to Paid)
        // surfaces as a typed transition exception so the API returns
        // 400 with the new status — not a silent overwrite.
        var current = await _repository.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (current is null) return null;
        throw new InvalidInvoiceTransitionException(current.Status, InvoiceStatus.Overdue);
    }

    public async Task<OverdueBatchResult> MarkEligibleOverdueAsync(
        Guid? tenantId,
        DateTime nowUtc,
        int take,
        CancellationToken ct = default)
    {
        if (take <= 0) return new OverdueBatchResult(0, 0, Array.Empty<OverdueBatchFailure>());

        // tenantId == null is the cross-tenant sweep used by the hosted
        // scheduler (which runs in a server scope, not a request scope, so
        // it has no X-Tenant-Id). Operator-triggered API calls pass their
        // calling tenant id and the search is scoped accordingly.
        var candidates = await _repository.GetInvoicesEligibleForOverdueAsync(
            tenantId: tenantId, nowUtc: nowUtc, take: take, ct: ct);

        var failures = new List<OverdueBatchFailure>();
        var updated = 0;
        var skipped = 0;

        foreach (var invoice in candidates)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Race-safe transition: the repo re-checks the
                // eligibility predicate (status in {Issued, PartiallyPaid}
                // AND date-boundary past-due) at write time. If a
                // concurrent payment moved the invoice to Paid (or another
                // writer voided it) between the eligibility query above
                // and this call, TryMarkOverdueAsync returns null and we
                // record a skip — we deliberately do NOT overwrite the
                // newer status.
                var result = await _repository.TryMarkOverdueAsync(
                    invoice.TenantId, invoice.Id, nowUtc, ct);
                if (result is null)
                {
                    // Conditional update found the row in a state that no
                    // longer matches eligibility (or the row vanished).
                    // Not a failure — the system is in a valid state and
                    // we just observed it concurrently advance.
                    skipped++;
                }
                else
                {
                    updated++;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Per-invoice failure isolation: one bad row does not
                // abort the batch. We capture only the message (no stack
                // trace) so the API response stays compact.
                failures.Add(new OverdueBatchFailure(invoice.TenantId, invoice.Id, ex.Message));
            }
        }

        return new OverdueBatchResult(updated, failures.Count, failures, skipped);
    }

    public async Task<RefundResult?> RefundAsync(
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        string? currency,
        string? reason,
        DateTime? refundedAt,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));
        if (amount <= 0m) throw new ArgumentException("Amount must be > 0.", nameof(amount));

        // Tenant-scoped lookup enforces ownership in the same query: cross-
        // tenant or unknown ids return null and surface as the same generic
        // "not found" response (no existence leak).
        var invoice = await _repository.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // Lifecycle gate: only Paid (or already PartiallyRefunded) invoices
        // can be refunded. Draft/Issued/PartiallyPaid/Overdue/Voided are not
        // valid sources for a refund — there is nothing to reverse, or the
        // invoice has been explicitly cancelled.
        if (!InvoiceStatus.AcceptsRefunds(invoice.Status))
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} in status '{invoice.Status}' cannot be refunded. " +
                $"Refundable statuses are: {InvoiceStatus.Paid}, {InvoiceStatus.PartiallyRefunded}.");
        }

        // Currency: default to invoice currency when unspecified, otherwise
        // require an exact match. We never silently convert.
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? invoice.Currency
            : currency.Trim().ToUpperInvariant();
        if (!string.Equals(normalizedCurrency, invoice.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refund currency '{normalizedCurrency}' does not match invoice currency '{invoice.Currency}'.");
        }

        var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        // Cap at "money actually collected on this invoice minus money
        // already returned". paidSum equals TotalAmount when the invoice
        // is Paid, but reading from Payments keeps this honest if a future
        // change ever lets a PartiallyPaid invoice be refunded.
        var paidSum = invoice.Payments.Sum(p => p.Amount);
        var existingRefunds = invoice.Refunds.Sum(r => r.Amount);
        var newRefundTotal = existingRefunds + roundedAmount;
        if (newRefundTotal > paidSum)
        {
            var remaining = paidSum - existingRefunds;
            throw new InvalidOperationException(
                $"Refund of {roundedAmount} would exceed the refundable balance on invoice {invoiceId}. " +
                $"Refundable balance is {remaining} (paid {paidSum}, already refunded {existingRefunds}).");
        }

        var now = DateTime.UtcNow;
        var refund = new Refund
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Amount = roundedAmount,
            Currency = normalizedCurrency,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            RefundedAt = refundedAt ?? now,
            CreatedAt = now,
        };

        var saved = await _refunds.AddAsync(refund, ct);

        // Drive the parent invoice's status from the new refund total. Full
        // reversal lands on the terminal Refunded state; anything less moves
        // (or keeps) the invoice in PartiallyRefunded so further refunds can
        // be appended.
        var newStatus = newRefundTotal >= paidSum
            ? InvoiceStatus.Refunded
            : InvoiceStatus.PartiallyRefunded;

        var updatedInvoice = invoice;
        if (newStatus != invoice.Status)
        {
            updatedInvoice = await _repository.UpdateStatusAsync(tenantId, invoiceId, newStatus, now, null, ct)
                ?? invoice;
        }

        return new RefundResult(saved, updatedInvoice);
    }
}
