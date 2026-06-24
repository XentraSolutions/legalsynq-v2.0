using Billing.Domain.Entities;
using Billing.Domain.Projections;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-WRITE-006 — single ledger row returned by
/// <c>GET /api/invoices/{id}/adjustments</c>. Read-only projection
/// over the immutable <see cref="InvoiceAdjustment"/> entity. No
/// mutable fields, no <c>CreatedBy</c> exposure (kept server-side
/// for audit only — see WRITE-005 report "Known gaps").
/// </summary>
public sealed record InvoiceAdjustmentLedgerItem(
    Guid Id,
    string Type,
    decimal Amount,
    string Currency,
    string Reason,
    string? ReferenceNumber,
    DateTime CreatedAt)
{
    public static InvoiceAdjustmentLedgerItem From(InvoiceAdjustment a)
        => new(
            Id: a.Id,
            Type: a.Type,
            Amount: a.Amount,
            Currency: a.Currency,
            Reason: a.Reason,
            ReferenceNumber: a.ReferenceNumber,
            CreatedAt: a.CreatedAt);
}

/// <summary>
/// MS-BILL-WRITE-006 — wire shape for
/// <c>GET /api/invoices/{id}/accounting-summary</c>. Mirrors the
/// <see cref="InvoiceAccountingSummary"/> projection 1:1; declared
/// here (not just re-exposing the projection record directly) so
/// future API-only fields (e.g. ETag, server timestamp) can be
/// added without touching the domain projection.
/// </summary>
public sealed record InvoiceAccountingSummaryResponse(
    Guid InvoiceId,
    string Currency,
    decimal InvoiceTotal,
    decimal PaidSum,
    decimal AdjustmentCreditSum,
    decimal AdjustmentDebitSum,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding)
{
    public static InvoiceAccountingSummaryResponse From(InvoiceAccountingSummary s)
        => new(
            InvoiceId: s.InvoiceId,
            Currency: s.Currency,
            InvoiceTotal: s.InvoiceTotal,
            PaidSum: s.PaidSum,
            AdjustmentCreditSum: s.AdjustmentCreditSum,
            AdjustmentDebitSum: s.AdjustmentDebitSum,
            EffectiveTotal: s.EffectiveTotal,
            EffectiveOutstanding: s.EffectiveOutstanding);
}
