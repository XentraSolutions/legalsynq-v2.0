using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;

namespace TenantBilling.Api.Contracts;

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
