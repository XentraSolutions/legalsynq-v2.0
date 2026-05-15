namespace Billing.Domain.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — Orchestrator entry point. Single seam between
/// the controller and the projection-builder + provider + repository.
///
/// <para>
/// Read-only with respect to Billing accounting rows: the
/// orchestrator NEVER mutates an invoice, payment, or adjustment.
/// It only INSERTs into <c>accounting_exports</c> (Pending), then
/// UPDATEs that single row to a terminal state.
/// </para>
/// </summary>
public interface IAccountingExportService
{
    /// <summary>
    /// Run an export. The orchestrator:
    /// <list type="number">
    ///   <item>builds the canonical payload from immutable Billing
    ///     rows;</item>
    ///   <item>computes the deterministic fingerprint;</item>
    ///   <item>checks for a previous successful run with the same
    ///     fingerprint and short-circuits with
    ///     <see cref="AccountingExportStatus.Duplicate"/> if found;</item>
    ///   <item>persists a Pending lifecycle row;</item>
    ///   <item>resolves the named provider, calls
    ///     <see cref="IAccountingExportProvider.ExportAsync"/>;</item>
    ///   <item>persists the terminal status + payload JSON.</item>
    /// </list>
    /// </summary>
    Task<AccountingExportRunResult> RunAsync(
        AccountingExportRunRequest request,
        CancellationToken ct = default);

    Task<AccountingExport?> GetAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AccountingExport>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
