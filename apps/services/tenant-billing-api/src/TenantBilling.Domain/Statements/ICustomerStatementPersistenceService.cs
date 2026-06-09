using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Statements;

/// <summary>
/// STAT-B02 — Write surface for persisted customer statements.
/// Wraps the STAT-B01 builder
/// (<see cref="ICustomerStatementService"/>) plus the new template
/// selection + snapshot persistence pipeline. Every successful
/// generation produces an immutable
/// <see cref="CustomerStatement"/> row.
///
/// Returns are tenant-scoped: cross-tenant ids surface as
/// <c>null</c> (mapped to 404 by the controller) and never leak
/// existence of another tenant's data.
/// </summary>
public interface ICustomerStatementPersistenceService
{
    /// <summary>
    /// Build, snapshot, and persist a statement for an explicit
    /// inclusive period. <paramref name="explicitTemplateId"/> picks
    /// a specific template; null falls back to the tenant's default
    /// (also nullable). <paramref name="renderHtml"/> determines
    /// whether the HTML snapshot is also captured at write time.
    /// Returns null when the customer does not exist in scope.
    /// </summary>
    Task<CustomerStatement?> GenerateAsync(
        Guid tenantId,
        Guid customerId,
        DateTime periodStart,
        DateTime periodEnd,
        Guid? explicitTemplateId,
        bool renderHtml,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience: build for an entire calendar month and persist.
    /// Composes <see cref="GenerateAsync"/> with the month's first
    /// and last days.
    /// </summary>
    Task<CustomerStatement?> GenerateMonthlyAsync(
        Guid tenantId,
        Guid customerId,
        int year,
        int month,
        Guid? explicitTemplateId,
        bool renderHtml,
        CancellationToken ct = default);

    Task<IReadOnlyList<CustomerStatement>> ListHistoryAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default);

    Task<CustomerStatement?> GetHistoryAsync(
        Guid tenantId, Guid statementId, CancellationToken ct = default);

    /// <summary>
    /// Return the HTML for a persisted statement. When
    /// <see cref="CustomerStatement.HtmlSnapshot"/> is non-null it is
    /// returned verbatim; otherwise the JSON snapshot is rehydrated
    /// and re-rendered. The render path NEVER consults current
    /// invoices / payments. Returns null when the statement does not
    /// exist in scope.
    /// </summary>
    Task<string?> RenderHtmlAsync(Guid tenantId, Guid statementId, CancellationToken ct = default);

    /// <summary>
    /// Soft-void a generated statement. Idempotent: voiding a
    /// voided statement returns it unchanged. Returns null when the
    /// statement does not exist in scope.
    /// </summary>
    Task<CustomerStatement?> VoidAsync(
        Guid tenantId, Guid statementId, string? reason, CancellationToken ct = default);
}
