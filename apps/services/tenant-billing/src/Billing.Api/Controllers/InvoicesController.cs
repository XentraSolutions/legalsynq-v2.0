using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Entities;
using Billing.Domain.Exceptions;
using Billing.Domain.Rendering;
using Billing.Domain.Repositories;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    private readonly IPaymentService _payments;
    private readonly IInvoiceAdjustmentService _adjustments;
    private readonly IInvoiceAdjustmentRepository _adjustmentRepository;
    private readonly IInvoiceAccountingSummaryService _accountingSummary;
    private readonly IInvoiceTemplateSelectionService _templateSelection;
    private readonly IInvoiceRenderService _render;
    private readonly ITenantContext _tenant;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        IInvoiceService service,
        IPaymentService payments,
        IInvoiceAdjustmentService adjustments,
        IInvoiceAdjustmentRepository adjustmentRepository,
        IInvoiceAccountingSummaryService accountingSummary,
        IInvoiceTemplateSelectionService templateSelection,
        IInvoiceRenderService render,
        ITenantContext tenant,
        ILogger<InvoicesController> logger)
    {
        _service = service;
        _payments = payments;
        _adjustments = adjustments;
        _adjustmentRepository = adjustmentRepository;
        _accountingSummary = accountingSummary;
        _templateSelection = templateSelection;
        _render = render;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpPost]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var lines = request.Lines
                .Select(l => new NewInvoiceLine(l.Description, l.Quantity, l.UnitPrice))
                .ToList();

            // INV-TPL-02: resolve the effective template ONCE up
            // front, then reuse the same selection result for both
            // (a) deriving DefaultDueDays when DueDate is omitted
            // (INV-TPL-01 hook) and (b) stamping the branding
            // snapshot on the new invoice. Doing the selection twice
            // would risk a race where a concurrent template edit
            // between the two reads stamped a different template
            // than the one whose DueDays we used.
            //
            // Selection chain (implemented by SelectForTenantInvoiceAsync):
            //   1. explicit request.InvoiceTemplateId (validated:
            //      must exist in this tenant's scope and be Active —
            //      otherwise InvoiceTemplateNotFound/NotSelectable
            //      surfaces as 400 via the existing
            //      InvalidOperationException catch below)
            //   2. tenant default
            //   3. null (creating an invoice without any template
            //      is a fully supported path)
            InvoiceTemplate? effectiveTemplate = await _templateSelection
                .SelectForTenantInvoiceAsync(_tenant.TenantId, request.InvoiceTemplateId, ct);

            // Resolve the effective DueDate. Caller-supplied wins;
            // when omitted, fall back to the resolved template's
            // DefaultDueDays. If neither path yields a value, return
            // 400 with a clear message rather than letting the
            // underlying service fail on default(DateTime).
            var effectiveDueDate = request.DueDate;
            if (effectiveDueDate is null)
            {
                if (effectiveTemplate?.DefaultDueDays is int days)
                {
                    effectiveDueDate = request.IssueDate.AddDays(days);
                }
                else
                {
                    return Problem(
                        detail: "DueDate is required: no default invoice template with DefaultDueDays is configured for this tenant.",
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }

            // Tenant comes exclusively from the X-Tenant-Id header (via the
            // tenant context) — request.TenantId is ignored even when sent.
            // The resolved template (or null) is forwarded so the service
            // stamps the snapshot in-line with the insert.
            var created = await _service.CreateAsync(
                _tenant.TenantId, request.CustomerId, request.InvoiceNumber,
                request.IssueDate, effectiveDueDate.Value, request.Currency, request.Notes,
                lines, request.TaxAmount, request.DiscountAmount,
                template: effectiveTemplate, ct: ct);

            var dto = InvoiceResponse.From(created);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DuplicateInvoiceNumberException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race fallback: pre-flight check passed but a concurrent insert
            // created the same (TenantId, InvoiceNumber) before SaveChanges.
            return Problem(
                detail: $"An invoice with InvoiceNumber '{request.InvoiceNumber}' already exists for this tenant.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Pomelo MySql surfaces duplicate-key as MySqlException Number 1062.
        // EF InMemory raises ArgumentException via SaveChanges for duplicate
        // PKs. We match on inner message as a defensive fallback so the same
        // 409 mapping holds for both providers.
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("1062");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        // Empty-id guard: callers occasionally hand us Guid.Empty (e.g. a
        // missing path segment that bound to default). Return 400 rather
        // than masquerading as a 404 — different debugging signal.
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var i = await _service.GetAsync(_tenant.TenantId, id, ct);
            return i is null ? NotFound() : Ok(InvoiceResponse.From(i));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// INV-TPL-03 — Build the structured render document for an
    /// invoice. The document is composed exclusively from the
    /// invoice's stored fields (including the INV-TPL-02 branding
    /// snapshot) — no live <c>InvoiceTemplate</c> row is touched —
    /// so the rendered shape is deterministic and survives a later
    /// edit, retire, or hard-delete of the source template. Returns
    /// 404 when the invoice does not exist or belongs to a different
    /// tenant.
    /// </summary>
    [HttpGet("{id:guid}/render")]
    [ProducesResponseType(typeof(InvoiceRenderDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Render(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var doc = await _render.BuildRenderDocumentAsync(_tenant.TenantId, id, ct);
            return doc is null ? NotFound() : Ok(doc);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// INV-TPL-03 — Server-rendered HTML view of an invoice. Returns
    /// <c>text/html</c>. The HTML is self-contained (inline styles,
    /// no external JS, no remote stylesheets) and uses the invoice's
    /// stamped branding snapshot only. Every user/admin-supplied text
    /// field is HTML-escaped before output.
    ///
    /// PDF generation is intentionally not exposed here — see the
    /// INV-TPL-03 report for the deferral rationale. Downstream
    /// consumers (future PDF, future email body, future preview UI)
    /// can run this HTML through their own conversion step.
    /// </summary>
    [HttpGet("{id:guid}/render/html")]
    [Produces("text/html")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenderHtml(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var html = await _render.RenderHtmlAsync(_tenant.TenantId, id, ct);
            if (html is null) return NotFound();
            return Content(html, "text/html");
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// List invoices for the calling tenant. Supports search (invoice
    /// number / notes), status, customer, date-range filters, and paging.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(InvoiceListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        CancellationToken ct,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var page1 = page < 1 ? 1 : page;
        var size = pageSize <= 0 ? 25 : (pageSize > 100 ? 100 : pageSize);

        var result = await _service.ListPagedAsync(
            _tenant.TenantId, search, status, customerId, fromDate, toDate, page1, size, ct);

        return Ok(InvoiceListResponse.From(result, page1, size));
    }

    /// <summary>
    /// MS-BILL-WRITE-004 — Unified tenant-admin lifecycle transition. The
    /// (current → target) edge is validated against the centralised
    /// <see cref="InvoiceLifecycleService"/> graph; dispatch then routes to
    /// the existing per-action method that owns the operational guards
    /// (Issue / Void / MarkOverdue / Reevaluate). This keeps the legal-
    /// transition matrix in exactly one place AND keeps the existing
    /// per-action endpoints untouched (they're not in the BFF allowlist
    /// and remain internal-only).
    ///
    /// Accounting safety: target=Paid is only honoured when recorded
    /// payments fully cover the invoice total; otherwise the call returns
    /// 400 with no mutation. Refund flow has its own dedicated endpoint
    /// and is intentionally not exposed here.
    ///
    /// Reason is required (1–1000 chars) and is captured in the structured
    /// audit log on both success and failure (alongside tenant id, invoice
    /// id, before / after status). Returns 404 when the invoice does not
    /// exist or belongs to a different tenant; 400 when the transition is
    /// illegal or an operational guard rejects.
    /// </summary>
    [HttpPost("{id:guid}/transition")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(InvoiceLifecycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Transition(
        Guid id,
        [FromBody] TransitionInvoiceRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        // Trim once for both the audit log and the service call so the
        // log line and the service-side guard agree on the bound length.
        var targetStatus = (request.TargetStatus ?? string.Empty).Trim();
        var reason = (request.Reason ?? string.Empty).Trim();

        try
        {
            var result = await _service.TransitionAsync(_tenant.TenantId, id, targetStatus, reason, ct);
            if (result is null)
            {
                _logger.LogInformation(
                    "MS-BILL-WRITE-004 invoice transition refused (404): tenant={TenantId} invoice={InvoiceId} target={Target} reasonLen={ReasonLen}",
                    _tenant.TenantId, id, targetStatus, reason.Length);
                return NotFound();
            }

            _logger.LogInformation(
                "MS-BILL-WRITE-004 invoice transition ok: tenant={TenantId} invoice={InvoiceId} from={From} to={To} reasonLen={ReasonLen}",
                _tenant.TenantId, id, result.PreviousStatus, result.Invoice.Status, reason.Length);
            return Ok(InvoiceLifecycleResponse.From(result.Invoice, result.PreviousStatus, $"transitioned to {result.Invoice.Status}"));
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation(
                "MS-BILL-WRITE-004 invoice transition rejected (400/arg): tenant={TenantId} invoice={InvoiceId} target={Target} reasonLen={ReasonLen} message={Message}",
                _tenant.TenantId, id, targetStatus, reason.Length, ex.Message);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            // Catches InvalidInvoiceTransitionException (illegal edge),
            // UnknownInvoiceStatusException (typo / legacy value), and
            // InvalidInvoiceStateException (operational guard reject —
            // e.g. balance not zero, due date not passed, payments
            // recorded on a void). All map to 400 with the typed message
            // exposed (no stack trace, no internal detail leak — every
            // exception ctor is well-formed for user-facing display).
            _logger.LogInformation(
                "MS-BILL-WRITE-004 invoice transition rejected (400/op): tenant={TenantId} invoice={InvoiceId} target={Target} reasonLen={ReasonLen} message={Message}",
                _tenant.TenantId, id, targetStatus, reason.Length, ex.Message);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Transition an invoice from Draft to Issued.</summary>
    [HttpPost("{id:guid}/issue")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(IssueInvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Issue(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var updated = await _service.IssueAsync(_tenant.TenantId, id, ct);
            return updated is null ? NotFound() : Ok(IssueInvoiceResponse.From(updated));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Transition an invoice to Voided (only if no payments exist).</summary>
    [HttpPost("{id:guid}/void")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Void(Guid id, CancellationToken ct)
        => RunTransition(id, () => _service.VoidAsync(_tenant.TenantId, id, ct));

    /// <summary>Recompute an invoice's status from its payments and DueDate.</summary>
    [HttpPost("{id:guid}/reevaluate")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Reevaluate(Guid id, CancellationToken ct)
        => RunTransition(id, () => _service.ReevaluateAsync(_tenant.TenantId, id, ct));

    /// <summary>
    /// Force-transition a single invoice to Overdue. The structural gate
    /// (Issued / PartiallyPaid → Overdue) and the operational gate (DueDate
    /// must have passed) are enforced inside <see cref="IInvoiceService.MarkOverdueAsync"/>.
    /// Returns 404 when the invoice does not exist or belongs to a
    /// different tenant; 400 when either gate rejects.
    /// </summary>
    [HttpPost("{id:guid}/mark-overdue")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(InvoiceLifecycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkOverdue(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        // Snapshot the previous status BEFORE the transition so the
        // lifecycle response can echo it back. We re-fetch via the same
        // tenant-scoped read the service uses, so a cross-tenant invoice
        // id surfaces here as null and we 404 without ever calling Mark.
        Domain.Entities.Invoice? before;
        try
        {
            before = await _service.GetAsync(_tenant.TenantId, id, ct);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        if (before is null) return NotFound();

        try
        {
            var updated = await _service.MarkOverdueAsync(_tenant.TenantId, id, ct);
            // updated == null only if the invoice disappeared between the
            // snapshot read and the mark call (effectively a race). Treat
            // as 404 — the caller's view of the world is no longer valid.
            return updated is null
                ? NotFound()
                : Ok(InvoiceLifecycleResponse.From(updated, before.Status, "marked overdue"));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Operator-triggered batch run: scan eligible invoices for the calling
    /// tenant and flip them to Overdue. <paramref name="take"/> caps the
    /// batch size (default 200, max 1000). Per-invoice failures are
    /// isolated and itemised in the response. Cross-tenant sweeps are the
    /// hosted scheduler's job — this endpoint is always tenant-scoped.
    /// </summary>
    [HttpPost("mark-overdue")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(OverdueBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkOverdueBatch(
        CancellationToken ct,
        [FromQuery] int take = 200)
    {
        // Clamp the batch size: callers control how big a chunk runs, but
        // we never let a single request churn more than 1000 invoices in
        // one transaction. Operators who need more should run multiple
        // batches.
        var clamped = take <= 0 ? 200 : (take > 1000 ? 1000 : take);

        var result = await _service.MarkEligibleOverdueAsync(
            tenantId: _tenant.TenantId,
            nowUtc: DateTime.UtcNow,
            take: clamped,
            ct: ct);

        return Ok(OverdueBatchResponse.From(result));
    }

    /// <summary>
    /// Record a refund against a Paid (or PartiallyRefunded) invoice. Full
    /// refund moves the invoice to Refunded; partial refund moves it to
    /// PartiallyRefunded.
    /// </summary>
    [HttpPost("{id:guid}/refund")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.PaymentWrite)]
    [ProducesResponseType(typeof(RefundInvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundInvoiceRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var result = await _service.RefundAsync(
                _tenant.TenantId, id, request.Amount, request.Currency,
                request.Reason, request.RefundedAt, ct);
            return result is null ? NotFound() : Ok(RefundInvoiceResponse.From(result));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// All payments recorded against an invoice, ordered newest first.
    /// Returns 404 when the invoice does not exist or belongs to a different
    /// tenant (no cross-tenant existence leak).
    /// </summary>
    [HttpGet("{id:guid}/payments")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayments(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var payments = await _payments.GetByInvoiceAsync(_tenant.TenantId, id, ct);
            return payments is null
                ? NotFound()
                : Ok(payments.Select(PaymentResponse.From).ToList());
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Money summary for an invoice (total, total paid, balance due, current
    /// status). Returns 404 when the invoice does not exist or belongs to a
    /// different tenant.
    /// </summary>
    [HttpGet("{id:guid}/payment-summary")]
    [ProducesResponseType(typeof(InvoicePaymentSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentSummary(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var summary = await _payments.GetInvoicePaymentSummaryAsync(_tenant.TenantId, id, ct);
            return summary is null
                ? NotFound()
                : Ok(InvoicePaymentSummaryResponse.From(summary));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// MS-BILL-WRITE-005 — append a Credit / Debit adjustment
    /// (a.k.a. credit memo) to an invoice. Append-only by contract:
    /// the original invoice totals, line items, and payment rows
    /// are immutable through this flow. The effective balance is
    /// recomputed on demand from
    /// <c>(TotalAmount + sum(Debit) - sum(Credit) - sum(Payments))</c>.
    /// Tenant-admin only — gated end-to-end at the BFF
    /// (admin session + origin allowlist + Idempotency-Key) before
    /// the request ever reaches Billing.Api. Tenant scoping is
    /// resolved from the injected <c>X-Tenant-Id</c> header.
    /// </summary>
    [HttpPost("{id:guid}/adjustments")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.InvoiceWrite)]
    [ProducesResponseType(typeof(InvoiceAdjustmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAdjustment(
        Guid id, [FromBody] CreateAdjustmentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        var tenantId = _tenant.TenantId;
        try
        {
            var result = await _adjustments.CreateAsync(
                tenantId: tenantId,
                invoiceId: id,
                type: request.Type,
                amount: request.Amount,
                reason: request.Reason,
                referenceNumber: request.ReferenceNumber,
                createdBy: null,
                ct: ct);

            if (result is null)
            {
                _logger.LogInformation(
                    "MS-BILL-WRITE-005 adjustment-not-found tenant={TenantId} invoice={InvoiceId}",
                    tenantId, id);
                return NotFound();
            }

            _logger.LogInformation(
                "MS-BILL-WRITE-005 adjustment-created tenant={TenantId} invoice={InvoiceId} adjustment={AdjustmentId} type={Type} amount={Amount} effectiveOutstanding={EffOut} reasonLen={ReasonLen}",
                tenantId, id, result.Adjustment.Id, result.Adjustment.Type,
                result.Adjustment.Amount, result.EffectiveOutstanding, result.Adjustment.Reason.Length);

            return Ok(InvoiceAdjustmentResponse.From(result));
        }
        catch (OverCreditException ex)
        {
            _logger.LogWarning(
                "MS-BILL-WRITE-005 adjustment-over-credit tenant={TenantId} invoice={InvoiceId} requested={Requested} owed={Owed} paid={Paid}",
                tenantId, id, ex.RequestedCredit, ex.EffectiveOwed, ex.PaidSum);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidAdjustmentTypeException ex)
        {
            _logger.LogWarning(
                "MS-BILL-WRITE-005 adjustment-invalid-type tenant={TenantId} invoice={InvoiceId} suppliedType={Type}",
                tenantId, id, ex.SuppliedType);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvoiceNotAdjustableException ex)
        {
            _logger.LogWarning(
                "MS-BILL-WRITE-005 adjustment-not-adjustable tenant={TenantId} invoice={InvoiceId} status={Status}",
                tenantId, id, ex.Status);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                "MS-BILL-WRITE-005 adjustment-bad-request tenant={TenantId} invoice={InvoiceId} reason={Reason}",
                tenantId, id, ex.Message);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// MS-BILL-WRITE-006 — read the immutable adjustment ledger for a
    /// single invoice. Tenant-scoped; returns 404 with no existence
    /// leak when the invoice does not exist or belongs to another
    /// tenant. Empty ledger surfaces as <c>[]</c> with HTTP 200.
    /// Ordered newest-first for the UI (the underlying repository
    /// returns chronological order, which we reverse here).
    /// </summary>
    [HttpGet("{id:guid}/adjustments")]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceAdjustmentLedgerItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAdjustments(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        var tenantId = _tenant.TenantId;

        // Tenant-scoped invoice fetch first so a missing / cross-tenant
        // id surfaces as 404 with no existence leak. The repository
        // call is cheap and indexed; doing it before the ledger read
        // also keeps the empty-array branch unambiguous (200 with []
        // means "invoice exists, no adjustments yet" — never "invoice
        // does not exist").
        var invoice = await _service.GetAsync(tenantId, id, ct);
        if (invoice is null)
        {
            _logger.LogInformation(
                "MS-BILL-WRITE-006 ledger-not-found tenant={TenantId} invoice={InvoiceId}",
                tenantId, id);
            return NotFound();
        }

        var rows = await _adjustmentRepository.GetByInvoiceAsync(tenantId, id, ct);
        // Newest-first for UI presentation. The repository returns
        // chronological (oldest-first) for audit-trail correctness;
        // reversing here keeps the audit invariant at the data layer.
        var items = rows
            .OrderByDescending(a => a.CreatedAt)
            .Select(InvoiceAdjustmentLedgerItem.From)
            .ToList();

        _logger.LogInformation(
            "MS-BILL-WRITE-006 ledger-fetched tenant={TenantId} invoice={InvoiceId} count={Count}",
            tenantId, id, items.Count);

        return Ok(items);
    }

    /// <summary>
    /// MS-BILL-WRITE-006 — accounting summary projection for a single
    /// invoice. Single authoritative read-side derivation of
    /// <c>(invoiceTotal, paidSum, adjustmentCreditSum,
    /// adjustmentDebitSum, effectiveTotal, effectiveOutstanding)</c>.
    /// Voided payments are excluded by the underlying repository
    /// contract. Returns 404 with no existence leak when the invoice
    /// does not exist or belongs to another tenant.
    /// </summary>
    [HttpGet("{id:guid}/accounting-summary")]
    [ProducesResponseType(typeof(InvoiceAccountingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAccountingSummary(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);

        var tenantId = _tenant.TenantId;
        try
        {
            var summary = await _accountingSummary.GetAsync(tenantId, id, ct);
            if (summary is null)
            {
                _logger.LogInformation(
                    "MS-BILL-WRITE-006 summary-not-found tenant={TenantId} invoice={InvoiceId}",
                    tenantId, id);
                return NotFound();
            }

            _logger.LogInformation(
                "MS-BILL-WRITE-006 summary-fetched tenant={TenantId} invoice={InvoiceId} effectiveOutstanding={EffOut} credit={Credit} debit={Debit} paid={Paid}",
                tenantId, id, summary.EffectiveOutstanding,
                summary.AdjustmentCreditSum, summary.AdjustmentDebitSum, summary.PaidSum);

            return Ok(InvoiceAccountingSummaryResponse.From(summary));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IActionResult> RunTransition(Guid id, Func<Task<Domain.Entities.Invoice?>> action)
    {
        if (id == Guid.Empty)
            return Problem(detail: "InvoiceId is required.", statusCode: StatusCodes.Status400BadRequest);
        try
        {
            var updated = await action();
            return updated is null ? NotFound() : Ok(InvoiceResponse.From(updated));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
