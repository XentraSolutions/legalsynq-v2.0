using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenantBilling.Api.Contracts;
using TenantBilling.Api.Tenancy;
using TenantBilling.Domain.Services;

namespace TenantBilling.Api.Controllers;

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
