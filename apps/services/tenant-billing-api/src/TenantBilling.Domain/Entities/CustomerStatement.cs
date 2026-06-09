namespace TenantBilling.Domain.Entities;

/// <summary>
/// STAT-B02 — Lifecycle status for a persisted
/// <see cref="CustomerStatement"/>. Generated is the success state;
/// Voided is a soft-cancel that leaves snapshot content intact for
/// audit purposes.
/// </summary>
public static class CustomerStatementStatus
{
    public const string Generated = "Generated";
    public const string Voided = "Voided";

    public static bool IsValid(string? value) =>
        value is Generated or Voided;
}

/// <summary>
/// STAT-B02 — Persisted, immutable snapshot of a customer statement.
/// Created exactly once per <c>POST .../generate</c> call by the
/// persistence service. After insert, only <see cref="Status"/>,
/// <see cref="VoidedAtUtc"/>, and <see cref="VoidReason"/> may
/// transition (and only Generated → Voided).
///
/// The snapshot stores both:
///   * <see cref="StatementSnapshotJson"/> — the full
///     <c>CustomerStatementDocument</c> from the STAT-B01 builder
///     serialised via <c>System.Text.Json</c>. The renderer reads
///     this verbatim and never re-derives from current invoices /
///     payments.
///   * <see cref="HtmlSnapshot"/> — optional pre-rendered HTML for
///     callers who passed <c>renderHtml=true</c> at generate time.
///     When present the render endpoint returns it directly; when
///     absent, the renderer rebuilds HTML from
///     <see cref="StatementSnapshotJson"/> on demand.
/// </summary>
public sealed class CustomerStatement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Per-tenant unique <c>STMT-YYYY-NNNNNN</c>.</summary>
    public string StatementNumber { get; set; } = string.Empty;

    /// <summary>The template used at generation time, if any.</summary>
    public Guid? TemplateId { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>One of <see cref="CustomerStatementStatus"/>.</summary>
    public string Status { get; set; } = CustomerStatementStatus.Generated;

    public string Currency { get; set; } = "USD";
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// Full <c>CustomerStatementDocument</c> serialised to JSON. The
    /// authoritative content of the statement; render paths read this
    /// instead of re-querying invoices / payments.
    /// </summary>
    public string StatementSnapshotJson { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the <see cref="StatementTemplate"/> in effect at
    /// generation time, serialised to JSON. <c>null</c> when the
    /// caller did not select a template and the tenant had no
    /// default. Mirrors the invoice-template stamping pattern from
    /// INV-TPL-02.
    /// </summary>
    public string? TemplateSnapshotJson { get; set; }

    /// <summary>
    /// Optional pre-rendered HTML. Populated only when the caller
    /// passed <c>renderHtml=true</c> at generate time so the audit
    /// trail captures the exact bytes that would be sent.
    /// </summary>
    public string? HtmlSnapshot { get; set; }

    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
}
