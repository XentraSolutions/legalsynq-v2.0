namespace Billing.Domain.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — Orchestration contract for the bulk-import
/// workflow. The controller is a thin adapter; ALL CSV-handling,
/// validation, conflict detection, persistence and audit-history
/// stamping live behind this single port so the service can be
/// unit-tested without the ASP.NET Core multipart pipeline.
/// </summary>
public interface IBulkMappingImportService
{
    /// <summary>
    /// Read-only validation pass. NEVER mutates any Billing row.
    /// Re-runs deterministically for the same input.
    /// </summary>
    Task<BulkImportPreviewResult> ValidateAsync(
        Guid tenantId,
        Stream csv,
        CancellationToken ct = default);

    /// <summary>
    /// Commit the operator-confirmed subset. Re-validates server-
    /// side and only persists rows that re-classify as Valid or
    /// Warning. Writes exactly one
    /// <see cref="BulkMappingImportHistory"/> audit row.
    /// </summary>
    Task<BulkImportCommitResult> CommitAsync(
        Guid tenantId,
        BulkImportCommitCommand command,
        string operatorDisplayName,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Read-only audit-history list, newest-first.
    /// </summary>
    Task<IReadOnlyList<BulkImportHistorySnapshot>> ListHistoryAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Build the deterministic CSV export of every active +
    /// disabled mapping for this tenant. Returns a UTF-8-encoded
    /// byte array; the controller wraps it in a
    /// <c>text/csv</c> response with a stable filename.
    /// </summary>
    Task<byte[]> ExportMappingsAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
