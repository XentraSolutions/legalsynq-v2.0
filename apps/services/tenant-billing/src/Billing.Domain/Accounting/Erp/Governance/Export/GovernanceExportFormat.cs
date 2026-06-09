namespace Billing.Domain.Accounting.Erp.Governance.Export;

/// <summary>
/// MS-BILL-ERP-008 — Wire format choices for the governance
/// export endpoints. The literal values match the
/// case-insensitive query-string token (<c>?format=csv</c> /
/// <c>?format=json</c>); unrecognised values default to
/// <see cref="Json"/> so a misspelt query never leaks an
/// HTML error page through the BFF.
/// </summary>
public enum GovernanceExportFormat
{
    /// <summary>JSON envelope (<c>application/json</c>).</summary>
    Json = 0,

    /// <summary>RFC 4180 CSV (<c>text/csv; charset=utf-8</c>).</summary>
    Csv = 1,
}

/// <summary>
/// MS-BILL-ERP-008 — Stable string identifiers for the five
/// governance panels. Used by:
///   - the controller route literal,
///   - the export filename prefix,
///   - the <c>X-Governance-Export-Type</c> response header,
///   - the <c>metadata.exportType</c> field of the JSON envelope.
///
/// New panels MUST be appended (never reused or renamed) so an
/// archived evidence file always identifies its source.
/// </summary>
public static class GovernanceExportPanel
{
    public const string Summary = "summary";
    public const string ExportTrends = "export-trends";
    public const string RemediationAging = "remediation-aging";
    public const string AuditTrail = "audit-trail";
    public const string DriftIndicators = "drift-indicators";
}

/// <summary>
/// MS-BILL-ERP-008 — Bumped whenever the column set of any CSV
/// export changes. The current value is echoed in the
/// <c>X-Governance-Export-Schema-Version</c> response header and
/// the JSON envelope's <c>metadata.schemaVersion</c> field so a
/// downstream evidence-archive tool can detect column drift.
/// </summary>
public static class GovernanceExportSchema
{
    public const int Version = 1;
}

/// <summary>
/// MS-BILL-ERP-008 — Helper for parsing the <c>?format=</c>
/// query token. Tolerant: case-insensitive, whitespace-tolerant,
/// missing/unknown → <see cref="GovernanceExportFormat.Json"/>.
/// </summary>
public static class GovernanceExportFormatParser
{
    public static GovernanceExportFormat Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return GovernanceExportFormat.Json;
        var trimmed = raw.Trim();
        if (string.Equals(trimmed, "csv", System.StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceExportFormat.Csv;
        }
        return GovernanceExportFormat.Json;
    }
}
