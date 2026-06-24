using System.ComponentModel.DataAnnotations;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;

namespace TenantBilling.Api.Contracts;

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
    string? Notes)
{
    public static PaymentResponse From(Payment p) => new(
        p.Id, p.TenantId, p.InvoiceId, p.Amount, p.Currency, p.Method,
        p.Status, p.TransactionReference, p.PaidAt, p.CreatedAt, p.Notes);
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
