using System.ComponentModel.DataAnnotations;
using TenantBilling.Domain.Entities;

namespace TenantBilling.Api.Contracts;

/// <summary>
/// STAT-B02 — Request body for
/// <c>POST /api/statements/customers/{customerId}/generate</c>.
/// </summary>
public sealed class GenerateStatementRequest
{
    [Required]
    public DateTime? PeriodStart { get; set; }

    [Required]
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// Optional override; null falls back to the tenant's default
    /// template (if any).
    /// </summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// When true the persistence service also captures the rendered
    /// HTML on the snapshot. Default false — most callers will
    /// render lazily via <c>GET .../render/html</c>.
    /// </summary>
    public bool RenderHtml { get; set; }
}

/// <summary>
/// STAT-B02 — Request body for
/// <c>POST /api/statements/customers/{customerId}/monthly/generate</c>.
/// </summary>
public sealed class GenerateMonthlyStatementRequest
{
    [Required, Range(1900, 2999)]
    public int? Year { get; set; }

    [Required, Range(1, 12)]
    public int? Month { get; set; }

    public Guid? TemplateId { get; set; }
    public bool RenderHtml { get; set; }
}

/// <summary>
/// STAT-B02 — Optional body for the void endpoint.
/// </summary>
public sealed class VoidStatementRequest
{
    [StringLength(1000)]
    public string? Reason { get; set; }
}

/// <summary>
/// STAT-B02 — Persisted statement detail view. Includes all
/// monetary aggregates and snapshot blobs so a caller can fully
/// re-render without a second round-trip.
/// </summary>
public sealed record CustomerStatementResponse(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    string StatementNumber,
    Guid? TemplateId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime GeneratedAtUtc,
    string Status,
    string Currency,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal OutstandingBalance,
    decimal TotalInvoiced,
    decimal TotalPaid,
    string StatementSnapshotJson,
    string? TemplateSnapshotJson,
    bool HasHtmlSnapshot,
    DateTime? VoidedAtUtc,
    string? VoidReason)
{
    public static CustomerStatementResponse From(CustomerStatement s) => new(
        Id: s.Id,
        TenantId: s.TenantId,
        CustomerId: s.CustomerId,
        StatementNumber: s.StatementNumber,
        TemplateId: s.TemplateId,
        PeriodStart: s.PeriodStart,
        PeriodEnd: s.PeriodEnd,
        GeneratedAtUtc: s.GeneratedAtUtc,
        Status: s.Status,
        Currency: s.Currency,
        OpeningBalance: s.OpeningBalance,
        ClosingBalance: s.ClosingBalance,
        OutstandingBalance: s.OutstandingBalance,
        TotalInvoiced: s.TotalInvoiced,
        TotalPaid: s.TotalPaid,
        StatementSnapshotJson: s.StatementSnapshotJson,
        TemplateSnapshotJson: s.TemplateSnapshotJson,
        HasHtmlSnapshot: !string.IsNullOrEmpty(s.HtmlSnapshot),
        VoidedAtUtc: s.VoidedAtUtc,
        VoidReason: s.VoidReason);
}

/// <summary>
/// STAT-B02 — Lightweight projection for the per-customer history
/// list. Excludes snapshot blobs to keep the response small.
/// </summary>
public sealed record CustomerStatementSummaryResponse(
    Guid Id,
    string StatementNumber,
    Guid? TemplateId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime GeneratedAtUtc,
    string Status,
    string Currency,
    decimal ClosingBalance,
    decimal OutstandingBalance,
    bool HasHtmlSnapshot,
    DateTime? VoidedAtUtc)
{
    public static CustomerStatementSummaryResponse From(CustomerStatement s) => new(
        Id: s.Id,
        StatementNumber: s.StatementNumber,
        TemplateId: s.TemplateId,
        PeriodStart: s.PeriodStart,
        PeriodEnd: s.PeriodEnd,
        GeneratedAtUtc: s.GeneratedAtUtc,
        Status: s.Status,
        Currency: s.Currency,
        ClosingBalance: s.ClosingBalance,
        OutstandingBalance: s.OutstandingBalance,
        HasHtmlSnapshot: !string.IsNullOrEmpty(s.HtmlSnapshot),
        VoidedAtUtc: s.VoidedAtUtc);
}
