using Billing.Domain.Reporting;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-WRITE-007 — wire shapes for the four reporting endpoints.
/// Each DTO mirrors its domain row 1:1; declared separately so the
/// API can evolve fields (ETag, server timestamp, currency
/// conversions) without touching the domain projection.
/// </summary>
public sealed record AccountingSummaryReportRowDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string Status,
    string Currency,
    decimal InvoiceTotal,
    decimal PaidSum,
    decimal AdjustmentCreditSum,
    decimal AdjustmentDebitSum,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding,
    DateTime IssueDate,
    DateTime DueDate)
{
    public static AccountingSummaryReportRowDto From(AccountingSummaryRow r) => new(
        r.InvoiceId, r.InvoiceNumber, r.CustomerId, r.CustomerName, r.Status,
        r.Currency, r.InvoiceTotal, r.PaidSum, r.AdjustmentCreditSum,
        r.AdjustmentDebitSum, r.EffectiveTotal, r.EffectiveOutstanding,
        r.IssueDate, r.DueDate);
}

public sealed record InvoiceAgingReportRowDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string Status,
    string Currency,
    decimal InvoiceTotal,
    decimal PaidSum,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding,
    DateTime DueDate,
    int DaysOverdue,
    string AgingBucket)
{
    public static InvoiceAgingReportRowDto From(InvoiceAgingRow r) => new(
        r.InvoiceId, r.InvoiceNumber, r.CustomerId, r.CustomerName, r.Status,
        r.Currency, r.InvoiceTotal, r.PaidSum, r.EffectiveTotal,
        r.EffectiveOutstanding, r.DueDate, r.DaysOverdue, r.AgingBucket);
}

public sealed record AdjustmentReportRowDto(
    Guid AdjustmentId,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string Type,
    decimal Amount,
    string Currency,
    string Reason,
    string? ReferenceNumber,
    DateTime CreatedAt)
{
    public static AdjustmentReportRowDto From(AdjustmentReportRow r) => new(
        r.AdjustmentId, r.InvoiceId, r.InvoiceNumber, r.CustomerId,
        r.CustomerName, r.Type, r.Amount, r.Currency, r.Reason,
        r.ReferenceNumber, r.CreatedAt);
}

public sealed record PaymentReportRowDto(
    Guid PaymentId,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    string? TransactionReference,
    DateTime PaidAt,
    bool Reversed,
    DateTime? ReversedAt)
{
    public static PaymentReportRowDto From(PaymentReportRow r) => new(
        r.PaymentId, r.InvoiceId, r.InvoiceNumber, r.CustomerId,
        r.CustomerName, r.Amount, r.Currency, r.Method, r.Status,
        r.TransactionReference, r.PaidAt, r.Reversed, r.ReversedAt);
}
