using Billing.Domain.Accounting.Erp.QuickBooks;

namespace Billing.Domain.Accounting.Erp.Reconciliation;

/// <summary>
/// MS-BILL-ERP-004 — Concrete read-only orchestrator. Combines the
/// repository's tenant-scoped aggregates into the wire-shaped
/// snapshots the controller surfaces.
///
/// <para>
/// Carries no provider call, no scheduler, no queue, no mutation.
/// Every public method is a deterministic projection over append-
/// only Billing state.
/// </para>
/// </summary>
public sealed class ErpReconciliationService : IErpReconciliationService
{
    /// <summary>
    /// Default rolling window used by both the provider-health
    /// classifier and the mapping-stale check. Seven days picks up
    /// the typical "weekly close" cadence without surfacing
    /// transient noise.
    /// </summary>
    public const int ProviderHealthWindowSeconds = 7 * 24 * 60 * 60;

    /// <summary>
    /// Default staleness window for mappings; a mapping that has
    /// never exported OR has not exported within this window
    /// surfaces under <c>StaleMappingCount</c>.
    /// </summary>
    public const int MappingStaleWindowDays = 30;

    /// <summary>
    /// Hard cap on the per-provider rolling-health scan and on the
    /// unmapped-customer probe. Bounds the worst-case row count
    /// returned to a single dashboard load.
    /// </summary>
    public const int ScanHardCap = 1000;

    /// <summary>
    /// Maximum page size accepted by the list endpoint. Mirrors the
    /// existing AccountingExportController cap.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Default page size when the caller omits the parameter.
    /// </summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Threshold beyond which a provider is classified as
    /// <c>Unavailable</c> (when the latest attempt is also
    /// <c>ProviderUnavailable</c>). Below this it is at most
    /// <c>Degraded</c>.
    /// </summary>
    public const int UnavailableConsecutiveThreshold = 3;

    private readonly IErpReconciliationRepository _repo;
    private readonly TimeProvider _time;

    public ErpReconciliationService(
        IErpReconciliationRepository repo,
        TimeProvider time)
    {
        _repo = repo;
        _time = time;
    }

    public async Task<ErpReconciliationSummary> GetSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        Require(tenantId);

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var staleBeforeUtc = nowUtc.AddDays(-MappingStaleWindowDays);

        var counts = await _repo.CountByStatusAsync(tenantId, ct).ConfigureAwait(false);

        var latestSuccess = await _repo
            .GetMostRecentByStatusAsync(tenantId, AccountingExportStatus.Exported, ct)
            .ConfigureAwait(false);
        var latestFailed = await _repo
            .GetMostRecentByStatusAsync(tenantId, AccountingExportStatus.Failed, ct)
            .ConfigureAwait(false);

        var unmapped = await _repo
            .CountUnmappedActiveCustomersAsync(tenantId, ScanHardCap, ct)
            .ConfigureAwait(false);
        var stale = await _repo
            .CountStaleMappingsAsync(tenantId, staleBeforeUtc, ct)
            .ConfigureAwait(false);

        return new ErpReconciliationSummary(
            TotalExports: SumCounts(counts),
            ExportedCount: GetCount(counts, AccountingExportStatus.Exported),
            FailedCount: GetCount(counts, AccountingExportStatus.Failed),
            DuplicateCount: GetCount(counts, AccountingExportStatus.Duplicate),
            ProviderUnavailableCount: GetCount(counts, AccountingExportStatus.ProviderUnavailable),
            SkippedCount: GetCount(counts, AccountingExportStatus.Skipped),
            PendingCount: GetCount(counts, AccountingExportStatus.Pending),
            LatestSuccessfulExport: latestSuccess is null ? null : Diagnose(latestSuccess),
            LatestFailedExport: latestFailed is null ? null : Diagnose(latestFailed),
            UnmappedActiveCustomerCount: unmapped,
            StaleMappingCount: stale,
            StaleWindowDays: MappingStaleWindowDays,
            ObservedAtUtc: nowUtc);
    }

    public async Task<IReadOnlyList<ErpExportDiagnostic>> ListExportsAsync(
        Guid tenantId,
        ErpReconciliationListQuery query,
        CancellationToken ct = default)
    {
        Require(tenantId);
        var (page, pageSize) = NormalisePaging(query.Page, query.PageSize);
        var status = NormaliseFilter(query.Status, AllowedStatuses);
        var provider = NormaliseFilter(query.Provider, allowed: null);

        var rows = await _repo
            .ListAsync(tenantId, page, pageSize, status, provider, ct)
            .ConfigureAwait(false);
        return rows.Select(Diagnose).ToList();
    }

    public async Task<ErpExportDiagnosticDetail?> GetExportDetailAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default)
    {
        Require(tenantId);
        var row = await _repo.GetByIdAsync(tenantId, exportId, ct).ConfigureAwait(false);
        if (row is null) return null;

        var siblings = string.IsNullOrEmpty(row.Fingerprint)
            ? 0
            : await _repo
                .CountSiblingsByFingerprintAsync(tenantId, row.Fingerprint, row.Id, ct)
                .ConfigureAwait(false);

        return new ErpExportDiagnosticDetail(
            Diagnostic: Diagnose(row),
            Reason: row.Reason,
            SiblingsByFingerprint: siblings);
    }

    public async Task<ErpMappingHealthSnapshot> GetMappingHealthAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        Require(tenantId);

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var staleBeforeUtc = nowUtc.AddDays(-MappingStaleWindowDays);

        var total = await _repo.CountAllMappingsAsync(tenantId, ct).ConfigureAwait(false);
        var active = await _repo
            .CountMappingsByStatusAsync(tenantId, QuickBooksCustomerMappingStatus.Active, ct)
            .ConfigureAwait(false);
        var inactive = await _repo
            .CountMappingsByStatusAsync(tenantId, QuickBooksCustomerMappingStatus.Disabled, ct)
            .ConfigureAwait(false);
        var unmapped = await _repo
            .CountUnmappedActiveCustomersAsync(tenantId, ScanHardCap, ct)
            .ConfigureAwait(false);
        var stale = await _repo
            .CountStaleMappingsAsync(tenantId, staleBeforeUtc, ct)
            .ConfigureAwait(false);
        var latest = await _repo
            .GetMostRecentlyExportedMappingAsync(tenantId, ct)
            .ConfigureAwait(false);

        return new ErpMappingHealthSnapshot(
            TotalMappings: total,
            ActiveMappings: active,
            InactiveMappings: inactive,
            UnmappedActiveCustomerCount: unmapped,
            StaleMappingCount: stale,
            StaleWindowDays: MappingStaleWindowDays,
            LatestExportedMapping: latest is null ? null : ToHealthRow(latest),
            ObservedAtUtc: nowUtc);
    }

    public async Task<ErpProviderHealthSnapshot> GetProviderHealthAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        Require(tenantId);

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var sinceUtc = nowUtc.AddSeconds(-ProviderHealthWindowSeconds);

        var recent = await _repo
            .ListRecentForProviderHealthAsync(tenantId, sinceUtc, ScanHardCap, ct)
            .ConfigureAwait(false);

        // Even if no rows fall inside the rolling window we still
        // want a baseline "Unknown" entry per provider that has
        // ever exported, so the dashboard renders something other
        // than an empty card. ListLatestPerProviderPerStatusAsync
        // gives us that bootstrap set.
        var bootstrap = await _repo
            .ListLatestPerProviderPerStatusAsync(tenantId, ct)
            .ConfigureAwait(false);

        var providerNames = recent.Select(r => r.Provider)
            .Concat(bootstrap.Select(r => r.Provider))
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var rows = providerNames
            .Select(p => ClassifyProvider(p, recent, bootstrap))
            .ToList();

        return new ErpProviderHealthSnapshot(
            WindowSeconds: ProviderHealthWindowSeconds,
            ObservedAtUtc: nowUtc,
            Providers: rows);
    }

    // ----------------------------------------------------------------

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        AccountingExportStatus.Pending,
        AccountingExportStatus.Exported,
        AccountingExportStatus.Failed,
        AccountingExportStatus.ProviderUnavailable,
        AccountingExportStatus.Skipped,
        AccountingExportStatus.Duplicate,
    };

    private static void Require(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
    }

    private static (int page, int pageSize) NormalisePaging(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;
        return (page, pageSize);
    }

    private static string? NormaliseFilter(string? raw, HashSet<string>? allowed)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (allowed is null) return trimmed.ToLowerInvariant();
        return allowed.Contains(trimmed) ? trimmed : null;
    }

    private static int SumCounts(IReadOnlyDictionary<string, int> counts)
    {
        var sum = 0;
        foreach (var v in counts.Values) sum += v;
        return sum;
    }

    private static int GetCount(IReadOnlyDictionary<string, int> counts, string key)
        => counts.TryGetValue(key, out var v) ? v : 0;

    private static ErpExportDiagnostic Diagnose(AccountingExport e)
        => new(
            ExportId: e.Id,
            Provider: e.Provider,
            ExportType: e.ExportType,
            Status: e.Status,
            WindowFromUtc: e.WindowFromUtc,
            WindowToUtc: e.WindowToUtc,
            CorrelationId: e.CorrelationId,
            ExternalReferenceId: e.ExternalReferenceId,
            FailureReason: e.FailureReason,
            RecordCount: e.InvoiceCount + e.PaymentCount + e.AdjustmentCount,
            InvoiceCount: e.InvoiceCount,
            PaymentCount: e.PaymentCount,
            AdjustmentCount: e.AdjustmentCount,
            JournalEntryCount: e.JournalEntryCount,
            RequestedBy: e.RequestedBy,
            RequestedAtUtc: e.RequestedAtUtc,
            CompletedAtUtc: e.CompletedAtUtc,
            FingerprintShort: ShortFingerprint(e.Fingerprint),
            IsDuplicate: string.Equals(e.Status, AccountingExportStatus.Duplicate, StringComparison.Ordinal));

    private static string ShortFingerprint(string fingerprint)
        => string.IsNullOrEmpty(fingerprint)
            ? string.Empty
            : (fingerprint.Length <= 12 ? fingerprint : fingerprint[..12]);

    private static ErpMappingHealthRow ToHealthRow(QuickBooksCustomerMapping m)
        => new(
            Id: m.Id,
            BillingCustomerId: m.BillingCustomerId,
            QuickBooksCustomerId: m.QuickBooksCustomerId,
            QuickBooksDisplayName: m.QuickBooksDisplayName,
            MappingStatus: m.MappingStatus,
            LastExportedAtUtc: m.LastExportedAtUtc);

    /// <summary>
    /// Deterministic per-provider classification. Walks the recent
    /// rows newest-first to count consecutive failures, then layers
    /// the bootstrap latest-per-status to surface the latest
    /// success / failure beyond the rolling window.
    /// </summary>
    private static ErpProviderHealthRow ClassifyProvider(
        string providerName,
        IReadOnlyList<AccountingExport> recent,
        IReadOnlyList<AccountingExport> bootstrap)
    {
        var providerRecent = recent
            .Where(r => string.Equals(r.Provider, providerName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.RequestedAtUtc)
            .ToList();
        var providerBootstrap = bootstrap
            .Where(r => string.Equals(r.Provider, providerName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        int successes = 0, failures = 0, unavailable = 0;
        foreach (var r in providerRecent)
        {
            if (r.Status == AccountingExportStatus.Exported) successes++;
            else if (r.Status == AccountingExportStatus.Failed) failures++;
            else if (r.Status == AccountingExportStatus.ProviderUnavailable) unavailable++;
        }

        // Consecutive failures from most-recent attempt walking back.
        var consecutive = 0;
        foreach (var r in providerRecent)
        {
            if (r.Status == AccountingExportStatus.Failed
                || r.Status == AccountingExportStatus.ProviderUnavailable)
                consecutive++;
            else
                break;
        }

        var latestSuccess = providerBootstrap
            .Where(r => r.Status == AccountingExportStatus.Exported)
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefault()
            ?.RequestedAtUtc;
        var latestFailureRow = providerBootstrap
            .Where(r => r.Status == AccountingExportStatus.Failed
                     || r.Status == AccountingExportStatus.ProviderUnavailable)
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefault();

        var latest = providerRecent.FirstOrDefault();
        string state;
        if (providerRecent.Count == 0)
        {
            state = ErpProviderHealthState.Unknown;
        }
        else if (latest is not null
            && latest.Status == AccountingExportStatus.ProviderUnavailable
            && consecutive >= UnavailableConsecutiveThreshold)
        {
            state = ErpProviderHealthState.Unavailable;
        }
        else if (failures > 0
            || unavailable > 0
            || (latest is not null && (latest.Status == AccountingExportStatus.Failed
                                    || latest.Status == AccountingExportStatus.ProviderUnavailable)))
        {
            state = ErpProviderHealthState.Degraded;
        }
        else
        {
            state = ErpProviderHealthState.Healthy;
        }

        return new ErpProviderHealthRow(
            Provider: providerName,
            State: state,
            RecentSuccesses: successes,
            RecentFailures: failures,
            RecentProviderUnavailable: unavailable,
            ConsecutiveFailures: consecutive,
            LatestSuccessAtUtc: latestSuccess,
            LatestFailureAtUtc: latestFailureRow?.RequestedAtUtc,
            LatestFailureReason: latestFailureRow?.FailureReason);
    }
}
