namespace Billing.Domain.Accounting.Erp.Reconciliation;

/// <summary>
/// MS-BILL-ERP-004 — Read-only tenant-scoped reconciliation data
/// access. Every method filters by <c>TenantId</c> at the SQL level
/// and uses <c>AsNoTracking</c>. NEVER mutates a row in
/// <c>accounting_exports</c>, <c>quickbooks_customer_mappings</c>,
/// or any other table — this is a diagnostics layer only.
/// </summary>
public interface IErpReconciliationRepository
{
    /// <summary>
    /// Per-status counts for the tenant's full export history. Used
    /// by <c>GET /summary</c>.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Most recent export with the given status (typically
    /// <c>Exported</c> or <c>Failed</c>). Returns null when none.
    /// </summary>
    Task<AccountingExport?> GetMostRecentByStatusAsync(
        Guid tenantId,
        string status,
        CancellationToken ct = default);

    /// <summary>
    /// Filtered listing newest-first. <paramref name="status"/> and
    /// <paramref name="provider"/> are case-insensitive equality
    /// filters when non-null/empty.
    /// </summary>
    Task<IReadOnlyList<AccountingExport>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? status,
        string? provider,
        CancellationToken ct = default);

    /// <summary>
    /// Single export by id, tenant-bound. Cross-tenant probe → null
    /// (controller maps to 404).
    /// </summary>
    Task<AccountingExport?> GetByIdAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default);

    /// <summary>
    /// Count of OTHER exports with the same fingerprint (excluding
    /// the one identified by <paramref name="exportId"/>). Used by
    /// the detail endpoint to expose replay/duplicate visibility
    /// without surfacing the payload.
    /// </summary>
    Task<int> CountSiblingsByFingerprintAsync(
        Guid tenantId,
        string fingerprint,
        Guid exportId,
        CancellationToken ct = default);

    /// <summary>
    /// Recent rows used for provider-health classification. Caller
    /// passes a UTC cut-off (typically now − windowSeconds). Returns
    /// newest-first; bounded by <paramref name="hardCap"/>.
    /// </summary>
    Task<IReadOnlyList<AccountingExport>> ListRecentForProviderHealthAsync(
        Guid tenantId,
        DateTime sinceUtc,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Per-provider latest-of-status (Exported / Failed /
    /// ProviderUnavailable). Used to enrich the provider-health row
    /// without scanning the entire history.
    /// </summary>
    Task<IReadOnlyList<AccountingExport>> ListLatestPerProviderPerStatusAsync(
        Guid tenantId,
        CancellationToken ct = default);

    // ---- Mapping-health ----------------------------------------

    Task<int> CountMappingsByStatusAsync(
        Guid tenantId,
        string mappingStatus,
        CancellationToken ct = default);

    Task<int> CountAllMappingsAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Number of NOT-deleted Billing customers without ANY mapping
    /// row (Active or Disabled). Bounded by <paramref name="hardCap"/>
    /// so a runaway tenant doesn't scan its entire customer table.
    /// </summary>
    Task<int> CountUnmappedActiveCustomersAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Active mappings whose <c>LastExportedAtUtc</c> is null OR
    /// strictly older than <paramref name="staleBeforeUtc"/>.
    /// </summary>
    Task<int> CountStaleMappingsAsync(
        Guid tenantId,
        DateTime staleBeforeUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Most recently touched mapping (by <c>LastExportedAtUtc</c>).
    /// Used as a "last activity" sample on the mapping-health card.
    /// </summary>
    Task<Billing.Domain.Accounting.Erp.QuickBooks.QuickBooksCustomerMapping?>
        GetMostRecentlyExportedMappingAsync(
            Guid tenantId,
            CancellationToken ct = default);
}
