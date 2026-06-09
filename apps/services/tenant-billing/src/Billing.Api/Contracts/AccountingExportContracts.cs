using Billing.Domain.Accounting.Erp;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-ERP-001 — Wire shape of the
/// <c>POST /api/erp/exports/run</c> request body.
///
/// <para>
/// The browser MUST NOT send a tenant id — the BFF resolves the
/// tenant from the IDM session and injects <c>X-Tenant-Id</c>
/// before forwarding. The browser MUST send an
/// <c>Idempotency-Key</c> header (enforced by the BFF write
/// protection bundle); the controller mirrors that header into
/// <see cref="IdempotencyKey"/> below for downstream
/// dedupe-record persistence on the lifecycle row.
/// </para>
/// </summary>
public sealed record AccountingExportRunRequestBody(
    string Provider,
    string ExportType,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    string? Reason);

/// <summary>
/// Wire shape of the <c>POST /api/erp/exports/run</c> response and
/// of the <c>GET /api/erp/exports/{id}</c> response.
/// </summary>
public sealed record AccountingExportResponse(
    Guid ExportId,
    string Provider,
    string ExportType,
    string Status,
    string? ExternalReferenceId,
    string CorrelationId,
    string? FailureReason,
    string RequestedBy,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    int InvoiceCount,
    int PaymentCount,
    int AdjustmentCount,
    int JournalEntryCount,
    string? Reason,
    bool WasDuplicate)
{
    public static AccountingExportResponse FromRunResult(
        AccountingExportRunResult r,
        string requestedBy,
        DateTime windowFromUtc,
        DateTime windowToUtc,
        string? reason)
        => new(
            ExportId: r.ExportId,
            Provider: r.Provider,
            ExportType: r.ExportType,
            Status: r.Status,
            ExternalReferenceId: r.ExternalReferenceId,
            CorrelationId: r.CorrelationId,
            FailureReason: r.FailureReason,
            RequestedBy: requestedBy,
            RequestedAtUtc: r.RequestedAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            WindowFromUtc: windowFromUtc,
            WindowToUtc: windowToUtc,
            InvoiceCount: r.InvoiceCount,
            PaymentCount: r.PaymentCount,
            AdjustmentCount: r.AdjustmentCount,
            JournalEntryCount: r.JournalEntryCount,
            Reason: reason,
            WasDuplicate: r.WasDuplicate);

    public static AccountingExportResponse FromEntity(AccountingExport e)
        => new(
            ExportId: e.Id,
            Provider: e.Provider,
            ExportType: e.ExportType,
            Status: e.Status,
            ExternalReferenceId: e.ExternalReferenceId,
            CorrelationId: e.CorrelationId,
            FailureReason: e.FailureReason,
            RequestedBy: e.RequestedBy,
            RequestedAtUtc: e.RequestedAtUtc,
            CompletedAtUtc: e.CompletedAtUtc,
            WindowFromUtc: e.WindowFromUtc,
            WindowToUtc: e.WindowToUtc,
            InvoiceCount: e.InvoiceCount,
            PaymentCount: e.PaymentCount,
            AdjustmentCount: e.AdjustmentCount,
            JournalEntryCount: e.JournalEntryCount,
            Reason: e.Reason,
            WasDuplicate: false);
}

/// <summary>
/// List envelope. Mirrors the OPS-002 / WRITE-007 response shape.
/// </summary>
public sealed record AccountingExportListResponse(
    int Page,
    int PageSize,
    int Count,
    IReadOnlyList<AccountingExportResponse> Items);

/// <summary>
/// Wire shape of <c>GET /api/erp/exports/{id}/payload</c> — the
/// canonical server-built payload as a single JSON blob, plus a
/// minimal envelope so the operator UI can render the payload
/// alongside the export's status and external reference without a
/// second round-trip.
/// </summary>
public sealed record AccountingExportPayloadResponse(
    Guid ExportId,
    string Provider,
    string Status,
    string? ExternalReferenceId,
    DateTime? CompletedAtUtc,
    string PayloadJson);
