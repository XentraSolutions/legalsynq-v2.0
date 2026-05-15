using Microsoft.EntityFrameworkCore;
using Billing.Domain.Accounting.Erp.BulkImport;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — concrete tenant-scoped persistence for the
/// per-import audit row. Append-only writes; reads are tenant-
/// filtered, paged, and ordered newest-first.
/// </summary>
public sealed class BulkMappingImportHistoryRepository : IBulkMappingImportHistoryRepository
{
    private readonly BillingDbContext _db;

    public BulkMappingImportHistoryRepository(BillingDbContext db)
    {
        _db = db;
    }

    public async Task AppendAsync(BulkMappingImportHistory row, CancellationToken ct = default)
    {
        if (row is null) throw new ArgumentNullException(nameof(row));
        if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
        await _db.BulkMappingImportHistory.AddAsync(row, ct).ConfigureAwait(false);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Detach the failed insert so the next call on this
            // DbContext does not retry it.
            _db.Entry(row).State = EntityState.Detached;
            throw new BulkMappingImportReplayException(
                "A bulk-import audit row already exists for this (TenantId, IdempotencyKey).");
        }
    }

    public async Task FinalizeAsync(
        Guid tenantId,
        Guid historyId,
        DateTime completedAtUtc,
        int acceptedRows,
        int rejectedRows,
        string summaryJson,
        CancellationToken ct = default)
    {
        var row = await _db.BulkMappingImportHistory
            .Where(x => x.TenantId == tenantId && x.Id == historyId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new InvalidOperationException(
                $"Audit row {historyId} not found for tenant {tenantId}; cannot finalize.");
        row.CompletedAtUtc = completedAtUtc;
        row.AcceptedRows = acceptedRows;
        row.RejectedRows = rejectedRows;
        row.SummaryJson = summaryJson;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Pomelo MySql surfaces duplicate-key as MySqlException
        // Number 1062. We string-match on the message so the
        // infrastructure layer doesn't need a hard reference to
        // the MySql provider exception type. Mirrors the helper
        // pattern in InvoicesController / PaymentsController.
        var msg = (ex.InnerException?.Message ?? ex.Message) ?? string.Empty;
        return msg.Contains("Duplicate entry")
            || msg.Contains("1062")
            || msg.Contains("UX_bulk_mapping_import_history_TenantId_IdempotencyKey");
    }

    public async Task<BulkMappingImportHistory?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;
        return await _db.BulkMappingImportHistory
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IdempotencyKey == idempotencyKey)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BulkMappingImportHistory>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        var skip = (page - 1) * pageSize;
        return await _db.BulkMappingImportHistory
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
