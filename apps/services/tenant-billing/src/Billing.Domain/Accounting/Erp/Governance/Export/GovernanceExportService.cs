using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Billing.Domain.Csv;

namespace Billing.Domain.Accounting.Erp.Governance.Export;

/// <summary>
/// MS-BILL-ERP-008 — Default <see cref="IGovernanceExportService"/>.
///
/// Composes the existing ERP-007
/// <see cref="IErpGovernanceAnalyticsService"/> projections and
/// serialises them into either RFC 4180 CSV (via the shared
/// <see cref="CsvWriter"/>) or a JSON envelope.
///
/// All five methods are read-only. No mutation, no QBO call, no
/// queue publish, no schedule.
/// </summary>
public sealed class GovernanceExportService : IGovernanceExportService
{
    private readonly IErpGovernanceAnalyticsService _analytics;
    private readonly TimeProvider _time;

    public GovernanceExportService(
        IErpGovernanceAnalyticsService analytics,
        TimeProvider time)
    {
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    // -----------------------------------------------------------
    // Per-panel column orderings. Hard-coded literals so a
    // refactor of the C# record cannot silently re-order CSVs
    // already in production. Order MUST match the projector
    // returned by the matching `*Rows` method.
    // -----------------------------------------------------------
    private static readonly string[] SummaryColumns =
    {
        "WindowDays",
        "WindowFromUtc",
        "WindowToUtc",
        "TotalExports",
        "ExportedCount",
        "FailedCount",
        "ProviderUnavailableCount",
        "DuplicateCount",
        "SkippedCount",
        "PendingCount",
        "ExportSuccessRatePercent",
        "FailedExportRatePercent",
        "ReplayRatePercent",
        "ActiveCustomerCount",
        "ActiveMappingCount",
        "InactiveMappingCount",
        "UnresolvedMappingCount",
        "MappingCoveragePercent",
        "AverageRemediationAgeDays",
        "InvoiceFirstMappingCount",
        "InvoiceFirstAdoptionPercent",
        "RecentGovernanceFailureCount",
        "ObservedAtUtc",
    };

    private static readonly string[] TrendColumns =
    {
        "BucketDateUtc",
        "Provider",
        "ExportType",
        "TotalCount",
        "ExportedCount",
        "FailedCount",
        "ProviderUnavailableCount",
        "DuplicateCount",
    };

    private static readonly string[] AgingColumns =
    {
        "BillingCustomerId",
        "BillingCustomerName",
        "CustomerCreatedAtUtc",
        "AgeDays",
        "LastInvoiceDate",
        "ExistingMappingStatus",
        "ExportBlockedReason",
    };

    private static readonly string[] AuditColumns =
    {
        "TimestampUtc",
        "ActionType",
        "Operator",
        "TargetEntityType",
        "TargetEntityId",
        "Result",
        "CorrelationId",
        "Detail",
    };

    private static readonly string[] DriftColumns =
    {
        "Category",
        "FingerprintShort",
        "Provider",
        "ExportType",
        "Occurrences",
        "LastSeenAtUtc",
        "LastFailureReason",
    };

    // -----------------------------------------------------------
    // Public surface
    // -----------------------------------------------------------

    public async Task<GovernanceExportPayload> ExportSummaryAsync(
        Guid tenantId, int? windowDays, GovernanceExportFormat format, CancellationToken ct = default)
    {
        var summary = await _analytics
            .GetSummaryAsync(tenantId, windowDays, ct)
            .ConfigureAwait(false);
        var meta = BuildMetadata(
            GovernanceExportPanel.Summary,
            summary.WindowDays, summary.WindowFromUtc, summary.WindowToUtc);

        if (format == GovernanceExportFormat.Csv)
        {
            var rows = new[] { SummaryRow(summary) };
            return BuildCsvPayload(meta, SummaryColumns, rows);
        }
        return BuildJsonPayload(meta, summary);
    }

    public async Task<GovernanceExportPayload> ExportTrendsAsync(
        Guid tenantId, int? windowDays, GovernanceExportFormat format, CancellationToken ct = default)
    {
        var trends = await _analytics
            .GetExportTrendsAsync(tenantId, windowDays, ct)
            .ConfigureAwait(false);
        var meta = BuildMetadata(
            GovernanceExportPanel.ExportTrends,
            trends.WindowDays, trends.WindowFromUtc, trends.WindowToUtc);

        if (format == GovernanceExportFormat.Csv)
        {
            var rows = trends.Buckets.Select(TrendRow);
            return BuildCsvPayload(meta, TrendColumns, rows);
        }
        return BuildJsonPayload(meta, trends);
    }

    public async Task<GovernanceExportPayload> ExportRemediationAgingAsync(
        Guid tenantId, int? windowDays, GovernanceExportFormat format, CancellationToken ct = default)
    {
        var aging = await _analytics
            .GetRemediationAgingAsync(tenantId, windowDays, ct)
            .ConfigureAwait(false);
        var winDays = aging.Velocity.WindowDays;
        var winTo = aging.ObservedAtUtc;
        var winFrom = winTo.AddDays(-winDays);
        var meta = BuildMetadata(
            GovernanceExportPanel.RemediationAging, winDays, winFrom, winTo);

        if (format == GovernanceExportFormat.Csv)
        {
            var rows = aging.Oldest.Select(AgingRow);
            return BuildCsvPayload(meta, AgingColumns, rows);
        }
        return BuildJsonPayload(meta, aging);
    }

    public async Task<GovernanceExportPayload> ExportAuditTrailAsync(
        Guid tenantId, int? windowDays, int? page, int? pageSize,
        GovernanceExportFormat format, CancellationToken ct = default)
    {
        var audit = await _analytics
            .GetAuditTrailAsync(tenantId, windowDays, page, pageSize, ct)
            .ConfigureAwait(false);
        var meta = BuildMetadata(
            GovernanceExportPanel.AuditTrail,
            audit.WindowDays, audit.WindowFromUtc, audit.WindowToUtc);

        if (format == GovernanceExportFormat.Csv)
        {
            var rows = audit.Entries.Select(AuditRow);
            return BuildCsvPayload(meta, AuditColumns, rows);
        }
        return BuildJsonPayload(meta, audit);
    }

    public async Task<GovernanceExportPayload> ExportDriftIndicatorsAsync(
        Guid tenantId, int? windowDays, GovernanceExportFormat format, CancellationToken ct = default)
    {
        var drift = await _analytics
            .GetDriftIndicatorsAsync(tenantId, windowDays, ct)
            .ConfigureAwait(false);
        var meta = BuildMetadata(
            GovernanceExportPanel.DriftIndicators,
            drift.WindowDays, drift.WindowFromUtc, drift.WindowToUtc);

        if (format == GovernanceExportFormat.Csv)
        {
            // Two source lists collapsed into one categorised
            // table: deterministic order is "RepeatedFailure"
            // first then "ReplayHeavy", preserving each list's
            // own ordering.
            var rows = drift.RepeatedFailures.Select(r => DriftRow("RepeatedFailure", r))
                .Concat(drift.ReplayHeavy.Select(r => DriftRow("ReplayHeavy", r)));
            return BuildCsvPayload(meta, DriftColumns, rows);
        }
        return BuildJsonPayload(meta, drift);
    }

    // -----------------------------------------------------------
    // Row projectors (CSV) — order MUST match the *Columns array.
    // -----------------------------------------------------------

    private static IReadOnlyList<string?> SummaryRow(ErpGovernanceSummary s) => new[]
    {
        s.WindowDays.ToString(CultureInfo.InvariantCulture),
        CsvWriter.FormatDateTime(s.WindowFromUtc),
        CsvWriter.FormatDateTime(s.WindowToUtc),
        s.TotalExports.ToString(CultureInfo.InvariantCulture),
        s.ExportedCount.ToString(CultureInfo.InvariantCulture),
        s.FailedCount.ToString(CultureInfo.InvariantCulture),
        s.ProviderUnavailableCount.ToString(CultureInfo.InvariantCulture),
        s.DuplicateCount.ToString(CultureInfo.InvariantCulture),
        s.SkippedCount.ToString(CultureInfo.InvariantCulture),
        s.PendingCount.ToString(CultureInfo.InvariantCulture),
        CsvWriter.FormatDecimal(s.ExportSuccessRatePercent),
        CsvWriter.FormatDecimal(s.FailedExportRatePercent),
        CsvWriter.FormatDecimal(s.ReplayRatePercent),
        s.ActiveCustomerCount.ToString(CultureInfo.InvariantCulture),
        s.ActiveMappingCount.ToString(CultureInfo.InvariantCulture),
        s.InactiveMappingCount.ToString(CultureInfo.InvariantCulture),
        s.UnresolvedMappingCount.ToString(CultureInfo.InvariantCulture),
        CsvWriter.FormatDecimal(s.MappingCoveragePercent),
        CsvWriter.FormatDecimal(s.AverageRemediationAgeDays),
        s.InvoiceFirstMappingCount.ToString(CultureInfo.InvariantCulture),
        CsvWriter.FormatDecimal(s.InvoiceFirstAdoptionPercent),
        s.RecentGovernanceFailureCount.ToString(CultureInfo.InvariantCulture),
        CsvWriter.FormatDateTime(s.ObservedAtUtc),
    };

    private static IReadOnlyList<string?> TrendRow(ErpExportTrendBucket b) => new[]
    {
        CsvWriter.FormatDate(b.BucketDateUtc),
        b.Provider,
        b.ExportType,
        b.TotalCount.ToString(CultureInfo.InvariantCulture),
        b.ExportedCount.ToString(CultureInfo.InvariantCulture),
        b.FailedCount.ToString(CultureInfo.InvariantCulture),
        b.ProviderUnavailableCount.ToString(CultureInfo.InvariantCulture),
        b.DuplicateCount.ToString(CultureInfo.InvariantCulture),
    };

    private static IReadOnlyList<string?> AgingRow(RemediationAgingRow r) => new[]
    {
        r.BillingCustomerId.ToString(),
        r.BillingCustomerName,
        CsvWriter.FormatDateTime(r.CustomerCreatedAtUtc),
        r.AgeDays.ToString(CultureInfo.InvariantCulture),
        r.LastInvoiceDate is null ? null : CsvWriter.FormatDate(r.LastInvoiceDate.Value),
        r.ExistingMappingStatus,
        r.ExportBlockedReason,
    };

    private static IReadOnlyList<string?> AuditRow(GovernanceAuditEntry e) => new[]
    {
        CsvWriter.FormatDateTime(e.TimestampUtc),
        e.ActionType,
        e.Operator,
        e.TargetEntityType,
        e.TargetEntityId,
        e.Result,
        e.CorrelationId,
        e.Detail,
    };

    private static IReadOnlyList<string?> DriftRow(string category, DriftFingerprintRow r) => new[]
    {
        category,
        r.FingerprintShort,
        r.Provider,
        r.ExportType,
        r.Occurrences.ToString(CultureInfo.InvariantCulture),
        CsvWriter.FormatDateTime(r.LastSeenAtUtc),
        r.LastFailureReason,
    };

    // -----------------------------------------------------------
    // Envelope helpers
    // -----------------------------------------------------------

    private GovernanceExportMetadata BuildMetadata(
        string exportType, int windowDays, DateTime windowFromUtc, DateTime windowToUtc)
        => new(
            ExportType: exportType,
            WindowDays: windowDays,
            WindowFromUtc: windowFromUtc,
            WindowToUtc: windowToUtc,
            GeneratedAtUtc: _time.GetUtcNow().UtcDateTime,
            SchemaVersion: GovernanceExportSchema.Version);

    private static GovernanceExportPayload BuildCsvPayload(
        GovernanceExportMetadata meta, string[] columns,
        IEnumerable<IReadOnlyList<string?>> rows)
    {
        var csv = CsvWriter.Write(columns, rows);
        var body = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(csv);
        return new GovernanceExportPayload(
            Metadata: meta,
            ContentType: "text/csv; charset=utf-8",
            Filename: BuildFilename(meta, "csv"),
            Body: body);
    }

    private static GovernanceExportPayload BuildJsonPayload<T>(
        GovernanceExportMetadata meta, T data)
    {
        var envelope = new { metadata = meta, data };
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        return new GovernanceExportPayload(
            Metadata: meta,
            ContentType: "application/json",
            Filename: BuildFilename(meta, "json"),
            Body: body);
    }

    private static string BuildFilename(GovernanceExportMetadata meta, string ext)
    {
        // Deterministic filename: "<panel>-<utc-iso>-w<window>.<ext>".
        // The ISO-8601 timestamp uses safe characters (digits, T,
        // hyphen) so every shipped browser accepts it without
        // Content-Disposition quoting issues.
        var ts = meta.GeneratedAtUtc.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        return $"erp-governance-{meta.ExportType}-w{meta.WindowDays}-{ts}.{ext}";
    }
}
