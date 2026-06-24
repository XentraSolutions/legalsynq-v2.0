namespace Billing.Domain.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — Tenant-scoped read/write repository for
/// <see cref="AccountingExport"/> lifecycle rows.
///
/// <para>
/// Read methods MUST apply <c>Where(x => x.TenantId == tenantId)</c>
/// at the SQL level and use <c>AsNoTracking</c>. Write methods MUST
/// NOT mutate any other Billing entity.
/// </para>
/// </summary>
public interface IAccountingExportRepository
{
    /// <summary>
    /// Atomic dedupe + reserve. Opens a serializable transaction,
    /// looks up any **non-failed** row (Pending, Exported, or
    /// Duplicate) for the supplied tenant+fingerprint, and:
    /// <list type="bullet">
    ///   <item>If found, returns it as the "existing slot owner" —
    ///     caller treats it as <see cref="AccountingExportStatus.Duplicate"/>.
    ///     The pre-reserved <paramref name="newPending"/> row is
    ///     NOT persisted in this case.</item>
    ///   <item>If not found, INSERTs <paramref name="newPending"/>
    ///     and commits. Returns <c>null</c>.</item>
    /// </list>
    /// The serializable isolation level + the
    /// <c>(TenantId, Fingerprint)</c> index makes the
    /// check-then-insert atomic under concurrent POSTs for the
    /// same window: the second writer either sees the first
    /// writer's Pending row (returns it) or blocks on the gap
    /// lock until the first writer commits, then sees it.
    /// <c>Failed</c> / <c>ProviderUnavailable</c> rows do NOT
    /// block — operators must be able to retry transient failures.
    /// </summary>
    Task<AccountingExport?> TryReserveSlotAsync(
        Guid tenantId,
        string fingerprint,
        AccountingExport newPending,
        CancellationToken ct = default);

    /// <summary>
    /// Single, idempotent terminal-state update: status, optional
    /// external reference, completed timestamp, optional failure
    /// reason, optional payload JSON, count fields. Caller MUST
    /// have already loaded the row via
    /// <see cref="GetByIdAsync"/>.
    /// </summary>
    Task UpdateTerminalAsync(
        AccountingExport export,
        CancellationToken ct = default);

    Task<AccountingExport?> GetByIdAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default);

    /// <summary>
    /// Single page of export rows for the tenant, newest first.
    /// </summary>
    Task<IReadOnlyList<AccountingExport>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    // ------------- Projection-window loaders -----------------------
    // Tenant-scoped, AsNoTracking, bounded by the requested window.
    // The projection builder consumes the rows verbatim — no EF
    // tracking, no lazy loading, no cross-tenant joins. Window
    // semantics are inclusive on the lower bound and EXCLUSIVE on
    // the upper bound (`>= from && < to`) — same convention as the
    // OPS-002 delivery-analytics window.

    /// <summary>
    /// Tenant-scoped invoices with <c>IssueDate</c> in
    /// <c>[from, to)</c>. Cap enforced by the orchestrator (default
    /// 5000); a tenant exceeding the cap is rejected with a
    /// <c>WindowTooLarge</c> failure rather than truncated.
    /// </summary>
    Task<IReadOnlyList<Billing.Domain.Entities.Invoice>> LoadInvoicesForWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped payments with <c>PaidAt</c> in
    /// <c>[from, to)</c>. Voided rows are INCLUDED here (the export
    /// payload carries them with <c>Reversed=true</c>) so a
    /// downstream ERP receives a complete audit trail; the journal-
    /// entry builder excludes them from cash-side debits.
    /// </summary>
    Task<IReadOnlyList<Billing.Domain.Entities.Payment>> LoadPaymentsForWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped adjustments with <c>CreatedAt</c> in
    /// <c>[from, to)</c>.
    /// </summary>
    Task<IReadOnlyList<Billing.Domain.Entities.InvoiceAdjustment>> LoadAdjustmentsForWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped customer-name lookup for the supplied ids.
    /// Returns a dictionary keyed by CustomerId. Missing customers
    /// (e.g. soft-deleted) yield no entry; the projection falls
    /// back to <c>"(unknown)"</c>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> LoadCustomerNamesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken ct = default);
}
