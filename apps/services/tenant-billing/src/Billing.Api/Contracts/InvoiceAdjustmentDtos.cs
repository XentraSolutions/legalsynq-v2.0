using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-WRITE-005 — request body for
/// <c>POST /api/invoices/{id}/adjustments</c>. The invoice id and
/// tenant id are intentionally absent — the id comes from the URL
/// path and the tenant from the validated session (the BFF injects
/// the trusted <c>X-Tenant-Id</c> header).
///
/// <see cref="Type"/> must be one of the values exposed by the
/// adjustment service ("Credit" or "Debit", case-insensitive on
/// input). <see cref="Amount"/> must be strictly positive — the sign
/// is implied by <see cref="Type"/>. <see cref="Reason"/> is
/// mandatory (1–1000 chars) and is captured in the structured audit
/// log on both success and failure. <see cref="ReferenceNumber"/> is
/// optional (operator-supplied internal credit memo / ticket id).
/// </summary>
public sealed class CreateAdjustmentRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Type is required.")]
    [StringLength(16, MinimumLength = 1)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Adjustment amount in the parent invoice's currency. Must be
    /// strictly positive; rounded to 2dp away-from-zero by the
    /// service. The DataAnnotation upper bound matches the service-
    /// layer <c>MaxAmount</c> (decimal(18,2) precision cap).
    /// </summary>
    [Range(typeof(decimal), "0.01", "99999999.99",
        ErrorMessage = "Amount must be between 0.01 and 99999999.99.")]
    public decimal Amount { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Reason is required.")]
    [StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(64)]
    public string? ReferenceNumber { get; set; }
}

/// <summary>
/// MS-BILL-WRITE-005 — response shape for the adjustment endpoint.
/// Carries the persisted adjustment plus the recomputed effective
/// totals so the UI can render the post-adjustment balance without
/// a follow-up GET. The original <see cref="TotalAmount"/> is
/// included alongside the effective figures so the operator can see
/// both the immutable invoice-issued amount AND the post-adjustment
/// balance side by side.
/// </summary>
public sealed record InvoiceAdjustmentResponse(
    Guid Id,
    Guid InvoiceId,
    Guid CustomerId,
    string Type,
    decimal Amount,
    string Currency,
    string Reason,
    string? ReferenceNumber,
    DateTime CreatedAt,
    decimal TotalAmount,
    decimal PaidSum,
    decimal AdjustmentSumCredit,
    decimal AdjustmentSumDebit,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding)
{
    public static InvoiceAdjustmentResponse From(InvoiceAdjustmentResult r)
        => new(
            Id: r.Adjustment.Id,
            InvoiceId: r.Adjustment.InvoiceId,
            CustomerId: r.Adjustment.CustomerId,
            Type: r.Adjustment.Type,
            Amount: r.Adjustment.Amount,
            Currency: r.Adjustment.Currency,
            Reason: r.Adjustment.Reason,
            ReferenceNumber: r.Adjustment.ReferenceNumber,
            CreatedAt: r.Adjustment.CreatedAt,
            TotalAmount: r.Invoice.TotalAmount,
            PaidSum: r.PaidSum,
            AdjustmentSumCredit: r.AdjustmentSumCredit,
            AdjustmentSumDebit: r.AdjustmentSumDebit,
            EffectiveTotal: r.EffectiveTotal,
            EffectiveOutstanding: r.EffectiveOutstanding);
}
