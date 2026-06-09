namespace Billing.Domain.Statements;

/// <summary>
/// STAT-B01 — Render-only document representing a tenant-scoped
/// customer statement for a closed time window. Mirrors the shape of
/// <see cref="Rendering.InvoiceRenderDocument"/>: a stable, deterministic
/// snapshot computed from existing invoices and payments. Never
/// persisted; never mutates anything.
///
/// All money fields are in <see cref="Currency"/>. STAT-B01 enforces
/// that every invoice and payment counted toward this statement shares
/// a single currency (otherwise the build call rejects with a
/// validation error) — see <c>STAT-B01-report.md §12</c> for the
/// rationale.
/// </summary>
public sealed record CustomerStatementDocument(
    Guid StatementId,
    Guid TenantId,
    Guid CustomerId,
    string CustomerName,
    string? CustomerEmail,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    DateTime GeneratedAtUtc,
    string Currency,
    decimal OpeningBalance,
    decimal TotalInvoiced,
    decimal TotalPaid,
    decimal TotalAdjustments,
    decimal ClosingBalance,
    decimal OutstandingBalance,
    IReadOnlyList<CustomerStatementTransaction> Transactions,
    IReadOnlyList<CustomerStatementOutstandingInvoice> OutstandingInvoices);

/// <summary>
/// STAT-B01 — A single line in the statement's transaction table.
/// Either an issued invoice (debit, balance ↑) or a recorded payment
/// (credit, balance ↓). Refunds and adjustments are out of scope for
/// this block.
/// </summary>
public sealed record CustomerStatementTransaction(
    DateTime TransactionDate,
    CustomerStatementTransactionType Type,
    Guid ReferenceId,
    string? ReferenceNumber,
    string Description,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance);

public enum CustomerStatementTransactionType
{
    Invoice,
    Payment
}

/// <summary>
/// STAT-B01 — A single outstanding invoice as of statement generation.
/// Included regardless of whether the invoice was issued inside the
/// reporting period — a long-stale invoice from before the period or
/// a brand-new invoice issued after the period both surface here as
/// long as <c>AmountDue > 0</c>. <c>DaysPastDue</c> is computed
/// against the statement's <c>GeneratedAtUtc</c> day boundary so a
/// future-dated invoice reports <c>0</c> rather than a negative
/// number.
/// </summary>
public sealed record CustomerStatementOutstandingInvoice(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime IssueDate,
    DateTime DueDate,
    string Status,
    string Currency,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountDue,
    int DaysPastDue);
