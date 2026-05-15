using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _service;
    private readonly ITenantContext _tenant;

    public PaymentsController(IPaymentService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    /// <summary>
    /// Record a payment against an invoice. Returns the recorded payment plus
    /// the invoice's post-payment money summary (total, total paid, balance
    /// due) so callers don't have to round-trip a second request.
    /// </summary>
    [HttpPost]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.PaymentWrite)]
    [ProducesResponseType(typeof(RecordPaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var created = await _service.CreateAsync(
                _tenant.TenantId, request.InvoiceId, request.Amount,
                request.Currency, request.Method,
                // Status is server-controlled — always Recorded on create.
                PaymentService.RecordedStatus,
                request.TransactionReference, request.PaidAt, request.Notes, ct);

            // Reload the invoice summary so the caller sees the post-payment
            // balance and the (possibly newly-Paid) invoice status without a
            // second round trip. Inside the same scope, this hits the same
            // DbContext so it sees the just-committed payment.
            var summary = await _service.GetInvoicePaymentSummaryAsync(_tenant.TenantId, created.InvoiceId, ct);

            var paymentDto = PaymentResponse.From(created);
            // summary should never be null here — the payment just succeeded
            // against this invoice — but null-guard defensively.
            var summaryDto = summary is null
                ? new InvoicePaymentSummaryResponse(
                    created.InvoiceId, string.Empty, string.Empty, 0m, created.Amount, 0m, created.Currency)
                : InvoicePaymentSummaryResponse.From(summary);

            var dto = new RecordPaymentResponse(paymentDto, summaryDto);
            return CreatedAtAction(nameof(GetById), new { id = paymentDto.Id }, dto);
        }
        catch (InvoiceNotFoundException ex)
        {
            // POST /api/payments references the invoice by id in the request
            // body, not in the URL path. An unresolvable InvoiceId is a bad
            // request payload (HTTP 400), not a missing endpoint resource
            // (HTTP 404). Returning 400 here also avoids leaking whether the
            // invoice exists in another tenant: cross-tenant lookups surface
            // through the same InvoiceNotFoundException as truly unknown ids.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DuplicatePaymentReferenceException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidPaymentAmountException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (CurrencyMismatchException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (OverpaymentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidInvoicePaymentStateException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            // Fallback for any not-yet-typed validation failure surfaced from
            // the service (e.g. tenant guard ArgumentExceptions wrapped as
            // InvalidOperationException by future code paths).
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race fallback: pre-flight check passed but a concurrent insert
            // (e.g. two webhook deliveries arriving simultaneously) created
            // the same (TenantId, TransactionReference) before SaveChanges.
            // The DB unique index rejects it; we surface the same 409.
            return Problem(
                detail: $"A payment with TransactionReference '{request.TransactionReference}' already exists for this tenant.",
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

    /// <summary>
    /// MS-BILL-WRITE-002 — reverse a previously-recorded manual payment.
    /// Flips the payment's lifecycle status from <c>"Recorded"</c> to
    /// <c>"Voided"</c> and appends append-only audit metadata
    /// (<c>ReversedAt</c>, <c>ReversalReason</c>) without mutating any
    /// of the original financial fields. The parent invoice's paid sum
    /// and lifecycle status are recomputed inside the same transaction.
    /// <para>
    /// Idempotency is dual-bound: the BFF requires an
    /// <c>Idempotency-Key</c> header on every call; the durable domain
    /// guarantee is the "already Voided" check below, which surfaces
    /// duplicate reversal attempts as a clean HTTP 409 with a tailored
    /// message rather than a phantom second action.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/reverse")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.PaymentWrite)]
    [ProducesResponseType(typeof(ReversePaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reverse(
        Guid id,
        [FromBody] ReversePaymentRequest request,
        CancellationToken ct)
    {
        // Empty-id guard mirrors GetById. A malformed path id is a 400 caller
        // error, not a missing resource — distinct debugging signal.
        if (id == Guid.Empty)
            return Problem(detail: "PaymentId is required.", statusCode: StatusCodes.Status400BadRequest);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var result = await _service.ReverseAsync(_tenant.TenantId, id, request.Reason, ct);
            return Ok(ReversePaymentResponse.From(result));
        }
        catch (PaymentNotFoundException)
        {
            // Cross-tenant probes surface through the SAME exception as
            // truly-missing ids — no existence leak. We deliberately do
            // not echo the id back in the body so the response payload
            // is byte-identical to a vanilla 404.
            return NotFound();
        }
        catch (PaymentAlreadyReversedException ex)
        {
            // Duplicate reversal attempt against a Voided row. Distinct
            // 409 message so the BFF audit log can tell duplicate-submit
            // (idempotency-by-domain) apart from wrong-state-reversal.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (PaymentNotReversibleException ex)
        {
            // Wrong-state reversal — e.g. a legacy "Pending" row.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidReversalReasonException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
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
    /// MS-BILL-WRITE-003 — edit the <see cref="Payment.Notes"/> field on an
    /// existing payment. Metadata-only mutation: NO financial field, NO
    /// lifecycle status, NO timestamp, NO reversal audit field is touched.
    /// Notes are editable on both Recorded and Voided payments so operators
    /// can clarify reversal context after the fact.
    /// <para>
    /// Idempotency: the BFF requires an <c>Idempotency-Key</c> header on
    /// every call (same write-protection bundle as
    /// <see cref="Reverse"/> and <see cref="Create"/>). The operation
    /// itself is inherently safe to retry — replacing notes with the same
    /// value is a no-op — so there is no domain-level "already updated"
    /// exception; the BFF gate is the network-level guarantee against
    /// double-submit.
    /// </para>
    /// </summary>
    [HttpPatch("{id:guid}/notes")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.PaymentWrite)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNotes(
        Guid id,
        [FromBody] UpdatePaymentNotesRequest request,
        CancellationToken ct)
    {
        // Empty-id guard mirrors GetById / Reverse. A malformed path id is
        // a 400 caller error, not a missing resource — distinct debugging
        // signal than a 404.
        if (id == Guid.Empty)
            return Problem(detail: "PaymentId is required.", statusCode: StatusCodes.Status400BadRequest);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var updated = await _service.UpdateNotesAsync(_tenant.TenantId, id, request.Notes, ct);
            return Ok(PaymentResponse.From(updated));
        }
        catch (PaymentNotFoundException)
        {
            // Cross-tenant probes surface through the SAME exception as
            // truly-missing ids — no existence leak. Body is intentionally
            // empty so the response is byte-identical to a vanilla 404.
            return NotFound();
        }
        catch (InvalidPaymentNotesException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        // Empty-id guard mirrors InvoicesController. Different debugging
        // signal than a 404: the caller sent a malformed id, not asked for
        // a real one that doesn't exist.
        if (id == Guid.Empty)
            return Problem(detail: "PaymentId is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var p = await _service.GetAsync(_tenant.TenantId, id, ct);
            return p is null ? NotFound() : Ok(PaymentResponse.From(p));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Tenant-scoped, paginated, filtered payment list. Filters: optional
    /// invoiceId, status, method, fromDate / toDate (bound on PaidAt). Page
    /// is clamped to >= 1, pageSize to [1, 100] (default 25).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaymentListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        CancellationToken ct,
        [FromQuery] Guid? invoiceId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? method = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var page1 = page < 1 ? 1 : page;
        var size = pageSize <= 0 ? 25 : (pageSize > 100 ? 100 : pageSize);

        var result = await _service.ListPagedAsync(
            _tenant.TenantId, invoiceId, status, method, fromDate, toDate, page1, size, ct);

        return Ok(PaymentListResponse.From(result, page1, size));
    }
}
