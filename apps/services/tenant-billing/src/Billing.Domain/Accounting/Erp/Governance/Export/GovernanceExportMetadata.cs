namespace Billing.Domain.Accounting.Erp.Governance.Export;

/// <summary>
/// MS-BILL-ERP-008 — Metadata envelope attached to every
/// governance export. Surfaced two ways:
///   1. As <c>X-Governance-Export-*</c> response headers on the
///      CSV download (so an archive tool that only sees the
///      file blob can still index it).
///   2. As the <c>metadata</c> block of the JSON envelope.
///
/// Carries NO tenant id, NO operator email, NO QBO token, NO
/// fingerprint, NO recipient PII. The fields are intentionally
/// scoped to the export-context only:
///   - <see cref="ExportType"/>      — one of <see cref="GovernanceExportPanel"/>.
///   - <see cref="WindowDays"/>      — already clamped by the ERP-007 service.
///   - <see cref="WindowFromUtc"/> / <see cref="WindowToUtc"/>
///                                   — already computed by the ERP-007 service.
///   - <see cref="GeneratedAtUtc"/>  — wall-clock at export time.
///   - <see cref="SchemaVersion"/>   — see <see cref="GovernanceExportSchema"/>.
/// </summary>
public sealed record GovernanceExportMetadata(
    string ExportType,
    int WindowDays,
    System.DateTime WindowFromUtc,
    System.DateTime WindowToUtc,
    System.DateTime GeneratedAtUtc,
    int SchemaVersion);

/// <summary>
/// MS-BILL-ERP-008 — Final wire envelope for a governance
/// export. The controller wraps the bytes in a
/// <c>FileContentResult</c> with <see cref="ContentType"/> and
/// returns <see cref="Filename"/> via Content-Disposition.
/// </summary>
public sealed record GovernanceExportPayload(
    GovernanceExportMetadata Metadata,
    string ContentType,
    string Filename,
    byte[] Body);
