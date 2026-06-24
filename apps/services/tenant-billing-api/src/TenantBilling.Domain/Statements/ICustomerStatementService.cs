namespace TenantBilling.Domain.Statements;

/// <summary>
/// STAT-B01 — Build a customer statement and (optionally) render it to
/// HTML. Pure read path: never mutates invoices, payments, customers,
/// or any other entity; never opens a transaction. Returns
/// <c>null</c> when the customer does not exist or belongs to a
/// different tenant — the controller maps that to a uniform 404 with
/// no cross-tenant existence leak.
///
/// Throws <see cref="StatementValidationException"/> for input-level
/// problems (date ordering, range cap, multi-currency activity).
/// </summary>
public interface ICustomerStatementService
{
    /// <summary>
    /// Compose the structured statement document for a customer over
    /// the closed inclusive period <c>[periodStart, periodEnd]</c>.
    /// </summary>
    Task<CustomerStatementDocument?> BuildStatementAsync(
        Guid tenantId,
        Guid customerId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default);

    /// <summary>
    /// Build the statement and pass it to the configured
    /// <see cref="ICustomerStatementHtmlRenderer"/>. Returns
    /// <c>null</c> when the underlying build returns null.
    /// </summary>
    Task<string?> RenderHtmlAsync(
        Guid tenantId,
        Guid customerId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default);
}
