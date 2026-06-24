namespace Billing.Domain.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — Audit row written exactly once per
/// <see cref="IBulkMappingImportService.CommitAsync"/> call. Surfaces
/// in the tenant-admin import-history table and lets the operator
/// see at a glance who imported what and how many rows landed.
///
/// <para>
/// Persisted on the <c>bulk_mapping_import_history</c> table with
/// a single tenant-scoped composite index on
/// <c>(TenantId, StartedAtUtc DESC)</c>. The <see cref="SummaryJson"/>
/// column carries a deterministic, bounded JSON snapshot of the
/// per-row commit outcomes so the history-detail drill-down can
/// reproduce a faithful audit trail without holding unbounded data.
/// </para>
/// </summary>
public sealed class BulkMappingImportHistory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public string OperatorDisplayName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int AcceptedRows { get; set; }
    public int WarningRows { get; set; }
    public int RejectedRows { get; set; }

    /// <summary>
    /// Operator-supplied <c>Idempotency-Key</c> header value for the
    /// commit call (truncated to 128 chars). Persisted with a unique
    /// <c>(TenantId, IdempotencyKey)</c> index so a replayed commit
    /// returns the prior result instead of re-executing — closing
    /// the bulk-replay hole.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Deterministic JSON encoding of the per-row commit outcomes
    /// (capped at the same row limit as the upload itself). Stored
    /// as a TEXT column rather than a JSON column to stay portable
    /// across MySQL minor versions.
    /// </summary>
    public string SummaryJson { get; set; } = "{}";
}
