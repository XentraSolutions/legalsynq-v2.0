using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Api.Contracts;

/// <summary>
/// Inbound payload for <c>POST /api/payments</c>. TenantId is sourced from
/// the X-Tenant-Id request header, never from the body. Status is
/// server-controlled (the lifecycle is Recorded → Voided) and therefore not
/// exposed on the create surface.
/// </summary>
public sealed class CreatePaymentRequest
{
    [Required] public Guid InvoiceId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required, MinLength(3), MaxLength(3)] public string Currency { get; set; } = "USD";

    [Required, MaxLength(64)] public string Method { get; set; } = string.Empty;

    [MaxLength(200)] public string? TransactionReference { get; set; }

    public DateTime? PaidAt { get; set; }

    [MaxLength(2000)] public string? Notes { get; set; }
}

public sealed record PaymentResponse(
    Guid Id,
    Guid TenantId,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    string? TransactionReference,
    DateTime PaidAt,
    DateTime CreatedAt,
    string? Notes,
    DateTime? ReversedAt,
    string? ReversalReason)
{
    public static PaymentResponse From(Payment p) => new(
        p.Id, p.TenantId, p.InvoiceId, p.Amount, p.Currency, p.Method,
        p.Status, p.TransactionReference, p.PaidAt, p.CreatedAt, p.Notes,
        // MS-BILL-WRITE-002 — append-only audit fields. Null on every
        // Recorded payment; non-null IFF Status == "Voided".
        p.ReversedAt, p.ReversalReason);
}

/// <summary>
/// Paginated payment list response. Wraps a page of payments plus the
/// pagination metadata callers need to render a UI pager.
/// </summary>
public sealed record PaymentListResponse(
    IReadOnlyList<PaymentResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public static PaymentListResponse From(PaymentPage page, int requestedPage, int pageSize)
    {
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(page.TotalCount / (double)pageSize);
        return new PaymentListResponse(
            page.Items.Select(PaymentResponse.From).ToList(),
            requestedPage, pageSize, page.TotalCount, totalPages);
    }
}

/// <summary>
/// Aggregate money view of an invoice. Surfaced by
/// <c>GET /api/invoices/{id}/payment-summary</c>.
/// </summary>
public sealed record InvoicePaymentSummaryResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    string InvoiceStatus,
    decimal InvoiceTotal,
    decimal TotalPaid,
    decimal BalanceDue,
    string Currency)
{
    public static InvoicePaymentSummaryResponse From(InvoicePaymentSummary s) => new(
        s.InvoiceId, s.InvoiceNumber, s.InvoiceStatus, s.InvoiceTotal,
        s.TotalPaid, s.BalanceDue, s.Currency);
}

/// <summary>
/// Returned by <c>POST /api/payments</c>. Bundles the freshly recorded
/// payment with the invoice's post-payment money summary so callers don't
/// have to round-trip a second request to learn the new balance.
/// </summary>
public sealed record RecordPaymentResponse(
    PaymentResponse Payment,
    InvoicePaymentSummaryResponse InvoiceSummary);

/// <summary>
/// MS-BILL-WRITE-002 — inbound payload for
/// <c>POST /api/payments/{id}/reverse</c>. The payment id is sourced from
/// the URL path (never from the body). TenantId is sourced from the
/// X-Tenant-Id request header (never from the body). Reason is mandatory
/// and trimmed by the service layer; the StringLength bound mirrors the
/// service-level <c>PaymentService.MaxReversalReasonLength</c> and the
/// EF column width.
/// </summary>
public sealed class ReversePaymentRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// MS-BILL-WRITE-002 — returned by <c>POST /api/payments/{id}/reverse</c>.
/// Bundles the now-Voided payment row with the freshly recomputed invoice
/// money summary (lower paid total, possibly demoted invoice status) so the
/// tenant portal can refresh its detail view in a single round trip — same
/// shape as <see cref="RecordPaymentResponse"/>.
/// </summary>
public sealed record ReversePaymentResponse(
    PaymentResponse Payment,
    InvoicePaymentSummaryResponse InvoiceSummary)
{
    public static ReversePaymentResponse From(ReversePaymentResult result) => new(
        PaymentResponse.From(result.Payment),
        InvoicePaymentSummaryResponse.From(result.Invoice));
}

/// <summary>
/// MS-BILL-WRITE-003 — inbound payload for
/// <c>PATCH /api/payments/{id}/notes</c>. The payment id is sourced from the
/// URL path (never from the body); TenantId is sourced from the
/// X-Tenant-Id request header (never from the body). Notes is OPTIONAL —
/// passing <c>null</c>, the empty string, or whitespace clears the existing
/// note. The StringLength bound mirrors the service-level
/// <c>PaymentService.MaxNotesLength</c> and the EF column width so the model
/// binder produces a clean 400 ValidationProblem before the request reaches
/// the service layer.
/// </summary>
public sealed class UpdatePaymentNotesRequest
{
    [StringLength(2000)] public string? Notes { get; set; }
}
