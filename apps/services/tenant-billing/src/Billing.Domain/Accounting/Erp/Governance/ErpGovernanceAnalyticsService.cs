namespace Billing.Domain.Accounting.Erp.Governance;

/// <summary>
/// MS-BILL-ERP-007 — Pure-composition service for the five
/// tenant-admin governance dashboards. ALL state is sourced from
/// <see cref="IErpGovernanceAnalyticsRepository"/>; this class
/// performs window clamping, ratio derivation, audit-trail
/// composition, and bounded result shaping. It NEVER mutates a
/// row, NEVER calls a provider, NEVER schedules work, NEVER
/// emits an event.
/// </summary>
public sealed class ErpGovernanceAnalyticsService : IErpGovernanceAnalyticsService
{
    /// <summary>Smallest window the API will honor (1 day).</summary>
    public const int MinWindowDays = 1;

    /// <summary>Largest window the API will honor (90 days).</summary>
    public const int MaxWindowDays = 90;

    /// <summary>Default window when the caller omits the parameter.</summary>
    public const int DefaultWindowDays = 7;

    /// <summary>
    /// Allow-listed window choices the BFF / UI may use directly.
    /// Anything else is silently clamped into range.
    /// </summary>
    public static IReadOnlyList<int> AllowedWindowDays { get; } = new[] { 1, 7, 30, 90 };

    /// <summary>Stale-mapping cutoff (mirrors ERP-004 default).</summary>
    public const int StaleMappingWindowDays = 30;

    /// <summary>Hard cap on the unresolved-customer aging table.</summary>
    public const int RemediationAgingHardCap = 50;

    /// <summary>Hard cap on each drift fingerprint table.</summary>
    public const int DriftFingerprintHardCap = 50;

    /// <summary>Default audit-trail page size when omitted.</summary>
    public const int DefaultAuditPageSize = 20;

    /// <summary>Maximum audit-trail page size accepted.</summary>
    public const int MaxAuditPageSize = 100;

    /// <summary>Threshold (≥) for "repeated failure" classification.</summary>
    public const int RepeatedFailureThreshold = 2;

    /// <summary>Threshold (≥) for "replay-heavy" classification.</summary>
    public const int ReplayHeavyThreshold = 2;

    private readonly IErpGovernanceAnalyticsRepository _repo;
    private readonly TimeProvider _clock;

    public ErpGovernanceAnalyticsService(
        IErpGovernanceAnalyticsRepository repo,
        TimeProvider clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task<ErpGovernanceSummary> GetSummaryAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var window = ClampWindow(windowDays);
        var fromUtc = nowUtc.AddDays(-window);

        var statusCounts = await _repo
            .GetExportCountsByStatusAsync(tenantId, fromUtc, nowUtc, ct)
            .ConfigureAwait(false);

        var totals = await _repo
            .GetMappingTotalsAsync(tenantId, ct)
            .ConfigureAwait(false);

        var avgAge = await _repo
            .GetAverageUnresolvedAgeDaysAsync(tenantId, nowUtc, ct)
            .ConfigureAwait(false);

        int CountOf(string status) =>
            statusCounts.TryGetValue(status, out var v) ? v : 0;

        var exported = CountOf(AccountingExportStatus.Exported);
        var failed = CountOf(AccountingExportStatus.Failed);
        var providerUnavailable = CountOf(AccountingExportStatus.ProviderUnavailable);
        var duplicate = CountOf(AccountingExportStatus.Duplicate);
        var skipped = CountOf(AccountingExportStatus.Skipped);
        var pending = CountOf(AccountingExportStatus.Pending);
        var total = exported + failed + providerUnavailable + duplicate + skipped + pending;

        var coveragePct = SafePercent(
            totals.ActiveMappingCount,
            totals.ActiveCustomerCount);
        var invoiceFirstAdoptionPct = SafePercent(
            totals.InvoiceFirstActiveMappingCount,
            totals.ActiveMappingCount);
        var successPct = SafePercent(exported, total);
        var failedPct = SafePercent(failed + providerUnavailable, total);
        var replayPct = SafePercent(duplicate, total);

        return new ErpGovernanceSummary(
            WindowDays: window,
            WindowFromUtc: fromUtc,
            WindowToUtc: nowUtc,
            TotalExports: total,
            ExportedCount: exported,
            FailedCount: failed,
            ProviderUnavailableCount: providerUnavailable,
            DuplicateCount: duplicate,
            SkippedCount: skipped,
            PendingCount: pending,
            ExportSuccessRatePercent: successPct,
            FailedExportRatePercent: failedPct,
            ReplayRatePercent: replayPct,
            ActiveCustomerCount: totals.ActiveCustomerCount,
            ActiveMappingCount: totals.ActiveMappingCount,
            InactiveMappingCount: totals.InactiveMappingCount,
            UnresolvedMappingCount: totals.UnresolvedMappingCount,
            MappingCoveragePercent: coveragePct,
            AverageRemediationAgeDays: avgAge,
            InvoiceFirstMappingCount: totals.InvoiceFirstActiveMappingCount,
            InvoiceFirstAdoptionPercent: invoiceFirstAdoptionPct,
            RecentGovernanceFailureCount: failed + providerUnavailable,
            ObservedAtUtc: nowUtc);
    }

    public async Task<ErpExportTrendResult> GetExportTrendsAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var window = ClampWindow(windowDays);
        var fromUtc = nowUtc.AddDays(-window);

        var rows = await _repo
            .GetExportTrendBucketsAsync(tenantId, fromUtc, nowUtc, ct)
            .ConfigureAwait(false);

        // Group repo rows (one per status) into per-(date,provider,exportType)
        // composite buckets the chart can render directly.
        var grouped = rows
            .GroupBy(r => (r.BucketDateUtc, r.Provider, r.ExportType))
            .Select(g =>
            {
                int CountOf(string s) => g.Where(x => x.Status == s).Sum(x => x.Count);
                var exported = CountOf(AccountingExportStatus.Exported);
                var failed = CountOf(AccountingExportStatus.Failed);
                var providerUnavailable = CountOf(AccountingExportStatus.ProviderUnavailable);
                var duplicate = CountOf(AccountingExportStatus.Duplicate);
                var totalCount = g.Sum(x => x.Count);
                return new ErpExportTrendBucket(
                    BucketDateUtc: g.Key.BucketDateUtc,
                    Provider: g.Key.Provider,
                    ExportType: g.Key.ExportType,
                    TotalCount: totalCount,
                    ExportedCount: exported,
                    FailedCount: failed,
                    ProviderUnavailableCount: providerUnavailable,
                    DuplicateCount: duplicate);
            })
            // Deterministic ordering — date asc, provider asc, exportType asc.
            .OrderBy(b => b.BucketDateUtc)
            .ThenBy(b => b.Provider, StringComparer.Ordinal)
            .ThenBy(b => b.ExportType, StringComparer.Ordinal)
            .ToList();

        return new ErpExportTrendResult(
            WindowDays: window,
            WindowFromUtc: fromUtc,
            WindowToUtc: nowUtc,
            Buckets: grouped);
    }

    public async Task<RemediationAgingResult> GetRemediationAgingAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var window = ClampWindow(windowDays);
        var fromUtc = nowUtc.AddDays(-window);

        var aging = await _repo
            .GetUnresolvedCustomerAgingAsync(tenantId, RemediationAgingHardCap, ct)
            .ConfigureAwait(false);

        var totals = await _repo
            .GetMappingTotalsAsync(tenantId, ct)
            .ConfigureAwait(false);

        var staleCount = await _repo
            .GetStaleMappingCountAsync(
                tenantId,
                nowUtc.AddDays(-StaleMappingWindowDays),
                ct)
            .ConfigureAwait(false);

        var avgAge = await _repo
            .GetAverageUnresolvedAgeDaysAsync(tenantId, nowUtc, ct)
            .ConfigureAwait(false);

        var bulk = await _repo
            .GetBulkImportVelocityAsync(tenantId, fromUtc, nowUtc, ct)
            .ConfigureAwait(false);

        var resolvedInWindow = await _repo
            .GetMappingsResolvedInWindowAsync(tenantId, fromUtc, nowUtc, ct)
            .ConfigureAwait(false);

        var rows = aging
            .Select(r => new RemediationAgingRow(
                BillingCustomerId: r.BillingCustomerId,
                BillingCustomerName: r.BillingCustomerName,
                CustomerCreatedAtUtc: r.CustomerCreatedAtUtc,
                AgeDays: AgeDaysBetween(r.CustomerCreatedAtUtc, nowUtc),
                LastInvoiceDate: r.LastInvoiceDate,
                ExistingMappingStatus: r.ExistingMappingStatus,
                ExportBlockedReason: r.ExistingMappingStatus is null
                    ? "NoMapping"
                    : "MappingDisabled"))
            .ToList();

        var oldest = rows.Count == 0 ? 0 : rows.Max(r => r.AgeDays);

        return new RemediationAgingResult(
            UnresolvedCount: totals.UnresolvedMappingCount,
            OldestAgeDays: oldest,
            AverageAgeDays: avgAge,
            StaleMappingCount: staleCount,
            StaleWindowDays: StaleMappingWindowDays,
            Velocity: new RemediationVelocity(
                WindowDays: window,
                MappingsResolvedInWindow: resolvedInWindow,
                BulkImportsInWindow: bulk.BulkImportCount,
                BulkImportAcceptedRowsInWindow: bulk.AcceptedRowsSum),
            Oldest: rows,
            ObservedAtUtc: nowUtc);
    }

    public async Task<GovernanceAuditResult> GetAuditTrailAsync(
        Guid tenantId,
        int? windowDays,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var window = ClampWindow(windowDays);
        var fromUtc = nowUtc.AddDays(-window);
        var pageNum = page is null or < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1
            ? DefaultAuditPageSize
            : Math.Min(pageSize.Value, MaxAuditPageSize);

        var mappings = await _repo
            .GetMappingAuditRowsAsync(tenantId, fromUtc, nowUtc, ct)
            .ConfigureAwait(false);
        var bulks = await _repo
            .GetBulkImportAuditRowsAsync(tenantId, fromUtc, nowUtc, ct)
            .ConfigureAwait(false);
        var exports = await _repo
            .GetExportAuditRowsAsync(tenantId, fromUtc, nowUtc, ct)
            .ConfigureAwait(false);

        var entries = new List<GovernanceAuditEntry>(
            mappings.Count + bulks.Count + exports.Count);

        foreach (var m in mappings)
        {
            entries.Add(new GovernanceAuditEntry(
                TimestampUtc: m.CreatedAtUtc,
                ActionType: GovernanceAuditActionType.MappingCreated,
                Operator: m.CreatedBy,
                TargetEntityType: "QuickBooksCustomerMapping",
                TargetEntityId: m.MappingId.ToString(),
                Result: m.MappingStatus,
                CorrelationId: null,
                Detail: null));

            // Surface a separate "updated" entry only when the row
            // genuinely changed after creation. Treat sub-second
            // jitter as part of the create.
            if (m.UpdatedAtUtc > m.CreatedAtUtc.AddSeconds(1))
            {
                entries.Add(new GovernanceAuditEntry(
                    TimestampUtc: m.UpdatedAtUtc,
                    ActionType: GovernanceAuditActionType.MappingUpdated,
                    Operator: m.CreatedBy,
                    TargetEntityType: "QuickBooksCustomerMapping",
                    TargetEntityId: m.MappingId.ToString(),
                    Result: m.MappingStatus,
                    CorrelationId: null,
                    Detail: null));
            }
        }

        foreach (var b in bulks)
        {
            entries.Add(new GovernanceAuditEntry(
                TimestampUtc: b.StartedAtUtc,
                ActionType: GovernanceAuditActionType.BulkImportCommitted,
                Operator: b.OperatorDisplayName,
                TargetEntityType: "BulkMappingImportHistory",
                TargetEntityId: b.Id.ToString(),
                Result: $"accepted={b.AcceptedRows};rejected={b.RejectedRows};total={b.TotalRows}",
                CorrelationId: null,
                Detail: null));
        }

        foreach (var e in exports)
        {
            entries.Add(new GovernanceAuditEntry(
                TimestampUtc: e.RequestedAtUtc,
                ActionType: GovernanceAuditActionType.ExportAttempt,
                Operator: e.RequestedBy,
                TargetEntityType: "AccountingExport",
                TargetEntityId: e.ExportId.ToString(),
                Result: e.Status,
                CorrelationId: e.CorrelationId,
                Detail: $"{e.Provider}:{e.ExportType}"));
        }

        // Deterministic ordering: timestamp DESC; then ActionType ASC;
        // then EntityId ASC. Same key drives total-count + page slice.
        var ordered = entries
            .OrderByDescending(x => x.TimestampUtc)
            .ThenBy(x => x.ActionType, StringComparer.Ordinal)
            .ThenBy(x => x.TargetEntityId, StringComparer.Ordinal)
            .ToList();

        var total = ordered.Count;
        var skip = (pageNum - 1) * size;
        var slice = skip >= total
            ? Array.Empty<GovernanceAuditEntry>()
            : ordered.Skip(skip).Take(size).ToArray();

        return new GovernanceAuditResult(
            Page: pageNum,
            PageSize: size,
            TotalCount: total,
            WindowDays: window,
            WindowFromUtc: fromUtc,
            WindowToUtc: nowUtc,
            Entries: slice);
    }

    public async Task<DriftIndicatorResult> GetDriftIndicatorsAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var window = ClampWindow(windowDays);
        var fromUtc = nowUtc.AddDays(-window);

        var repeated = await _repo
            .GetRepeatedFailureFingerprintsAsync(
                tenantId, fromUtc, nowUtc, DriftFingerprintHardCap, ct)
            .ConfigureAwait(false);

        var replays = await _repo
            .GetReplayHeavyFingerprintsAsync(
                tenantId, fromUtc, nowUtc, DriftFingerprintHardCap, ct)
            .ConfigureAwait(false);

        var staleCount = await _repo
            .GetStaleMappingCountAsync(
                tenantId,
                nowUtc.AddDays(-StaleMappingWindowDays),
                ct)
            .ConfigureAwait(false);

        var totals = await _repo
            .GetMappingTotalsAsync(tenantId, ct)
            .ConfigureAwait(false);

        var (lastReason, lastAt) = await _repo
            .GetMostRecentFailureAsync(tenantId, ct)
            .ConfigureAwait(false);

        return new DriftIndicatorResult(
            WindowDays: window,
            WindowFromUtc: fromUtc,
            WindowToUtc: nowUtc,
            RepeatedFailureCount: repeated.Count,
            StaleMappingCount: staleCount,
            StaleWindowDays: StaleMappingWindowDays,
            ReplayHeavyCount: replays.Count,
            UnresolvedMappingCount: totals.UnresolvedMappingCount,
            LastGovernanceFailureReason: lastReason,
            LastGovernanceFailureAtUtc: lastAt,
            RepeatedFailures: repeated.Select(ToDriftRow).ToList(),
            ReplayHeavy: replays.Select(ToDriftRow).ToList(),
            ObservedAtUtc: nowUtc);
    }

    private static DriftFingerprintRow ToDriftRow(FingerprintCountRow r) =>
        new(
            FingerprintShort: r.Fingerprint.Length >= 12
                ? r.Fingerprint[..12]
                : r.Fingerprint,
            Provider: r.Provider,
            ExportType: r.ExportType,
            Occurrences: r.Count,
            LastSeenAtUtc: r.LastSeenAtUtc,
            LastFailureReason: r.LastFailureReason);

    /// <summary>
    /// Snap the caller-supplied window to the allow-list
    /// <see cref="AllowedWindowDays"/> (= {1, 7, 30, 90}); default
    /// to <see cref="DefaultWindowDays"/> when null. Out-of-range
    /// or arbitrary values (e.g. 14, 45, 1000) are normalized to
    /// the nearest allowed bucket *not exceeding* the request, so
    /// callers always receive deterministic, contract-pinned
    /// window sizes (the response echoes the chosen value back).
    /// Values below the minimum snap up to <see cref="MinWindowDays"/>
    /// and values above the maximum snap down to
    /// <see cref="MaxWindowDays"/>.
    /// </summary>
    public static int ClampWindow(int? windowDays)
    {
        if (windowDays is null) return DefaultWindowDays;
        var v = windowDays.Value;
        if (v <= MinWindowDays) return MinWindowDays;
        if (v >= MaxWindowDays) return MaxWindowDays;
        // Snap down to the largest allowed bucket <= v so callers
        // never receive a window stricter than they asked for.
        var snapped = MinWindowDays;
        foreach (var allowed in AllowedWindowDays)
        {
            if (allowed <= v && allowed > snapped) snapped = allowed;
        }
        return snapped;
    }

    /// <summary>
    /// Two-decimal percent of <paramref name="numerator"/>/
    /// <paramref name="denominator"/>; returns 0 when the
    /// denominator is 0 (so the dashboard does not show NaN).
    /// </summary>
    public static decimal SafePercent(int numerator, int denominator)
    {
        if (denominator <= 0) return 0m;
        var pct = (decimal)numerator * 100m / denominator;
        return Math.Round(pct, 2, MidpointRounding.AwayFromZero);
    }

    private static int AgeDaysBetween(DateTime fromUtc, DateTime nowUtc)
    {
        var diff = nowUtc - fromUtc;
        if (diff.TotalDays <= 0) return 0;
        return (int)Math.Floor(diff.TotalDays);
    }
}
