namespace Billing.Domain.Accounting.Erp.Remediation;

/// <summary>
/// MS-BILL-ERP-005 — Remediation orchestration. Reads the
/// unmapped-customer projection, brokers governed QBO customer
/// search, and validates a candidate mapping WITHOUT mutating any
/// row. Persistence is intentionally out of scope: the controller
/// reuses the existing ERP-003
/// <see cref="QuickBooks.IQuickBooksCustomerMappingService"/> POST
/// after explicit operator confirmation.
/// </summary>
public interface IErpRemediationService
{
    Task<IReadOnlyList<UnmappedCustomerRow>> ListUnmappedCustomersAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<QuickBooksCustomerSearchResult> SearchQuickBooksCustomersAsync(
        string query,
        CancellationToken ct = default);

    Task<MappingValidationResult> ValidateMappingAsync(
        Guid tenantId,
        MappingValidationCommand command,
        CancellationToken ct = default);
}
