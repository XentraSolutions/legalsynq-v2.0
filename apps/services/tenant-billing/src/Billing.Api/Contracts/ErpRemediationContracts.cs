using System.ComponentModel.DataAnnotations;
using Billing.Domain.Accounting.Erp.Remediation;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-ERP-005 — wire-format contracts for the tenant-admin
/// ERP remediation surface. None of these types echo a tenant id,
/// QBO token, refresh token, client secret, realm id, or account
/// ref to the browser.
/// </summary>
public sealed record UnmappedCustomerRowResponse(
    Guid BillingCustomerId,
    string BillingCustomerName,
    DateTime? LastInvoiceDate,
    string? LastExportFailureReason,
    DateTime? LastExportFailureAtUtc,
    string? ExportBlockedReason,
    string? ExistingMappingStatus)
{
    public static UnmappedCustomerRowResponse From(UnmappedCustomerRow r)
        => new(
            BillingCustomerId: r.BillingCustomerId,
            BillingCustomerName: r.BillingCustomerName,
            LastInvoiceDate: r.LastInvoiceDate,
            LastExportFailureReason: r.LastExportFailureReason,
            LastExportFailureAtUtc: r.LastExportFailureAtUtc,
            ExportBlockedReason: r.ExportBlockedReason,
            ExistingMappingStatus: r.ExistingMappingStatus);
}

public sealed record UnmappedCustomerListResponse(
    int Count,
    IReadOnlyList<UnmappedCustomerRowResponse> Items);

public sealed record QuickBooksCustomerSearchHitResponse(
    string QuickBooksCustomerId,
    string DisplayName,
    bool Active,
    string? PrimaryEmail);

public sealed record QuickBooksCustomerSearchResponse(
    string Outcome,
    int Count,
    IReadOnlyList<QuickBooksCustomerSearchHitResponse> Hits,
    string? FailureReason)
{
    public static QuickBooksCustomerSearchResponse From(QuickBooksCustomerSearchResult r)
    {
        var hits = (r.Hits ?? Array.Empty<QuickBooksCustomerSearchHit>())
            .Select(h => new QuickBooksCustomerSearchHitResponse(
                h.QuickBooksCustomerId, h.DisplayName, h.Active, h.PrimaryEmail))
            .ToList();
        return new QuickBooksCustomerSearchResponse(
            Outcome: r.Outcome.ToString(),
            Count: hits.Count,
            Hits: hits,
            FailureReason: r.FailureReason);
    }
}

public sealed class ValidateMappingRequestBody
{
    [Required] public Guid BillingCustomerId { get; set; }
    [Required, StringLength(100, MinimumLength = 1)]
    public string QuickBooksCustomerId { get; set; } = string.Empty;
}

public sealed record MappingValidationIssueResponse(string Code, string Message);

public sealed record MappingValidationResponse(
    string Outcome,
    string? QuickBooksDisplayName,
    IReadOnlyList<MappingValidationIssueResponse> Issues)
{
    public static MappingValidationResponse From(MappingValidationResult r)
        => new(
            Outcome: r.Outcome,
            QuickBooksDisplayName: r.QuickBooksDisplayName,
            Issues: r.Issues
                .Select(i => new MappingValidationIssueResponse(i.Code, i.Message))
                .ToList());
}
