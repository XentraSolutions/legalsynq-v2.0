using System.ComponentModel.DataAnnotations;
using Billing.Domain.Accounting.Erp.QuickBooks;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-ERP-003 — wire-format contracts for the
/// QuickBooks customer-mapping admin surface.
/// </summary>
public sealed record QuickBooksCustomerMappingResponse(
    Guid Id,
    Guid BillingCustomerId,
    string QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string MappingStatus,
    string? ExportMode,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastExportedAtUtc)
{
    public static QuickBooksCustomerMappingResponse From(QuickBooksCustomerMapping e)
        => new(
            Id: e.Id,
            BillingCustomerId: e.BillingCustomerId,
            QuickBooksCustomerId: e.QuickBooksCustomerId,
            QuickBooksDisplayName: e.QuickBooksDisplayName,
            MappingStatus: e.MappingStatus,
            ExportMode: e.ExportMode,
            CreatedBy: e.CreatedBy,
            CreatedAtUtc: e.CreatedAtUtc,
            UpdatedAtUtc: e.UpdatedAtUtc,
            LastExportedAtUtc: e.LastExportedAtUtc);
}

public sealed record QuickBooksCustomerMappingListResponse(
    int Page,
    int PageSize,
    int Count,
    IReadOnlyList<QuickBooksCustomerMappingResponse> Items);

public sealed class CreateQuickBooksCustomerMappingRequestBody
{
    [Required] public Guid BillingCustomerId { get; set; }
    [Required, StringLength(100, MinimumLength = 1)]
    public string QuickBooksCustomerId { get; set; } = string.Empty;
    [StringLength(200)] public string? QuickBooksDisplayName { get; set; }
    [Required, StringLength(32)] public string MappingStatus { get; set; } = QuickBooksCustomerMappingStatus.Active;
    [StringLength(32)] public string? ExportMode { get; set; }
}

public sealed class UpdateQuickBooksCustomerMappingRequestBody
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string QuickBooksCustomerId { get; set; } = string.Empty;
    [StringLength(200)] public string? QuickBooksDisplayName { get; set; }
    [Required, StringLength(32)] public string MappingStatus { get; set; } = QuickBooksCustomerMappingStatus.Active;
    [StringLength(32)] public string? ExportMode { get; set; }
}
