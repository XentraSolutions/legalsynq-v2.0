using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-WRITE-004 — request body for
/// <c>POST /api/invoices/{id}/transition</c>. The invoice id and
/// tenant id are intentionally absent — the id comes from the URL
/// path and the tenant from the validated session (the BFF injects
/// the trusted <c>X-Tenant-Id</c> header). <see cref="TargetStatus"/>
/// must be one of the values exposed by the unified transition
/// matrix (Issued, Voided, Overdue, Paid); refund flow has its own
/// dedicated endpoint and is not accepted here. <see cref="Reason"/>
/// is mandatory (1–1000 chars) and is captured in the structured
/// audit log on both success and failure.
/// </summary>
public sealed class TransitionInvoiceRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "TargetStatus is required.")]
    [StringLength(40, MinimumLength = 1)]
    public string TargetStatus { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Reason is required.")]
    [StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Slim response shape returned by lifecycle transition endpoints (e.g.
/// <c>POST /api/invoices/{id}/mark-overdue</c>). Carries just the
/// before/after status pair plus the timestamps a caller needs to confirm
/// the transition. Avoids reshipping the full invoice payload (line items,
/// payments, etc.) on every state change.
/// </summary>
public sealed record InvoiceLifecycleResponse(
    Guid Id,
    string InvoiceNumber,
    string PreviousStatus,
    string CurrentStatus,
    DateTime UpdatedAt,
    DateTime? IssuedAt,
    string? Message)
{
    public static InvoiceLifecycleResponse From(
        Invoice invoice, string previousStatus, string? message = null)
        => new(invoice.Id, invoice.InvoiceNumber, previousStatus, invoice.Status,
            invoice.UpdatedAt, invoice.IssuedAt, message);
}

/// <summary>
/// API-shaped variant of <see cref="OverdueBatchFailure"/>. Renamed for
/// API ergonomics (the JSON consumer doesn't know about domain types).
/// </summary>
public sealed record OverdueBatchFailureResponse(
    Guid TenantId,
    Guid InvoiceId,
    string Reason)
{
    public static OverdueBatchFailureResponse From(OverdueBatchFailure f)
        => new(f.TenantId, f.InvoiceId, f.Reason);
}

/// <summary>
/// Result of <c>POST /api/invoices/mark-overdue</c>. <c>UpdatedCount</c>
/// is the number of invoices flipped to Overdue in this run;
/// <c>SkippedCount</c> is the number that were eligible at list time but
/// concurrently advanced (e.g. a payment landed before the conditional
/// update ran) and are now in a newer valid state — these are not
/// failures; <c>FailedCount</c> is the number that hit per-invoice
/// exceptions (itemised in <see cref="Failures"/>).
/// </summary>
public sealed record OverdueBatchResponse(
    int UpdatedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<OverdueBatchFailureResponse> Failures)
{
    public static OverdueBatchResponse From(OverdueBatchResult r)
        => new(r.UpdatedCount, r.SkippedCount, r.FailedCount,
            r.Failures.Select(OverdueBatchFailureResponse.From).ToList());
}
