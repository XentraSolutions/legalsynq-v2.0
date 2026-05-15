using System.ComponentModel.DataAnnotations;
using Billing.Domain.Accounting.Erp.BulkImport;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-ERP-006 — wire-format contracts for the tenant-admin
/// bulk customer-mapping import / export / history surface. None
/// of these types echo a tenant id, QBO token, refresh token,
/// client secret, realm id, or account ref to the browser.
/// </summary>
public sealed record BulkImportRowIssueResponse(string Code, string Message)
{
    public static BulkImportRowIssueResponse From(BulkImportRowIssue i)
        => new(i.Code, i.Message);
}

public sealed record ValidatedBulkImportRowResponse(
    int LineNumber,
    Guid? BillingCustomerId,
    string? BillingCustomerName,
    string? QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string? ExportMode,
    string? Notes,
    string Classification,
    IReadOnlyList<BulkImportRowIssueResponse> Issues)
{
    public static ValidatedBulkImportRowResponse From(ValidatedBulkImportRow r)
        => new(
            r.LineNumber,
            r.BillingCustomerId,
            r.BillingCustomerName,
            r.QuickBooksCustomerId,
            r.QuickBooksDisplayName,
            r.ExportMode,
            r.Notes,
            r.Classification.ToString(),
            r.Issues.Select(BulkImportRowIssueResponse.From).ToList());
}

public sealed record BulkImportPreviewResponse(
    Guid PreviewToken,
    int TotalRows,
    int ValidCount,
    int WarningCount,
    int RejectedCount,
    IReadOnlyList<ValidatedBulkImportRowResponse> Rows,
    IReadOnlyList<BulkImportRowIssueResponse> DocumentIssues)
{
    public static BulkImportPreviewResponse From(BulkImportPreviewResult r)
        => new(
            r.PreviewToken,
            r.TotalRows,
            r.ValidCount,
            r.WarningCount,
            r.RejectedCount,
            r.Rows.Select(ValidatedBulkImportRowResponse.From).ToList(),
            r.DocumentIssues.Select(BulkImportRowIssueResponse.From).ToList());
}

public sealed class BulkImportCommitRowRequestBody
{
    [Required] public int LineNumber { get; set; }
    [Required] public Guid BillingCustomerId { get; set; }
    [Required, StringLength(100, MinimumLength = 1)]
    public string QuickBooksCustomerId { get; set; } = string.Empty;
    [StringLength(200)]
    public string? QuickBooksDisplayName { get; set; }
    [StringLength(32)]
    public string? ExportMode { get; set; }
    [StringLength(1000)]
    public string? Notes { get; set; }
}

public sealed class BulkImportCommitRequestBody
{
    public Guid? PreviewToken { get; set; }
    [Required, MinLength(1)]
    public List<BulkImportCommitRowRequestBody> Rows { get; set; } = new();
}

public sealed record BulkImportCommitRowResponse(
    int LineNumber,
    Guid BillingCustomerId,
    string QuickBooksCustomerId,
    string Outcome,
    Guid? MappingId,
    string? Error)
{
    public static BulkImportCommitRowResponse From(BulkImportCommitRowResult r)
        => new(r.LineNumber, r.BillingCustomerId, r.QuickBooksCustomerId, r.Outcome, r.MappingId, r.Error);
}

public sealed record BulkImportCommitResponse(
    Guid HistoryId,
    int TotalRequested,
    int Persisted,
    int Conflicted,
    int Rejected,
    int Failed,
    IReadOnlyList<BulkImportCommitRowResponse> Rows)
{
    public static BulkImportCommitResponse From(BulkImportCommitResult r)
        => new(
            r.HistoryId,
            r.TotalRequested,
            r.Persisted,
            r.Conflicted,
            r.Rejected,
            r.Failed,
            r.Rows.Select(BulkImportCommitRowResponse.From).ToList());
}

public sealed record BulkImportHistoryRowResponse(
    Guid Id,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string OperatorDisplayName,
    int TotalRows,
    int AcceptedRows,
    int WarningRows,
    int RejectedRows)
{
    public static BulkImportHistoryRowResponse From(BulkImportHistorySnapshot s)
        => new(
            s.Id,
            s.StartedAtUtc,
            s.CompletedAtUtc,
            s.OperatorDisplayName,
            s.TotalRows,
            s.AcceptedRows,
            s.WarningRows,
            s.RejectedRows);
}

public sealed record BulkImportHistoryListResponse(
    int Count,
    IReadOnlyList<BulkImportHistoryRowResponse> Items);
