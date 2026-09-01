using System.Text.Json.Serialization;

namespace Liens.Application.DTOs;

public sealed class CasePaymentQuery
{
    public string? Search { get; init; }
    public string? PaymentMethod { get; init; }
    public string? PostingStatus { get; init; }
    public string SortBy { get; init; } = "paymentDate";
    public string SortDirection { get; init; } = "desc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class CasePaymentListResponse
{
    public CasePaymentSummary Summary { get; init; } = new();
    public List<CasePaymentItemResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class CasePaymentSummary
{
    public decimal LienSellingAmount { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal RemainingBalance { get; init; }
    public decimal OverpaidAmount { get; init; }
    public int? LienAgingDays { get; init; }
    public string Currency { get; init; } = "USD";
}

public sealed class CasePaymentItemResponse
{
    public Guid Id { get; init; }
    public Guid? ReceiptId { get; init; }
    public Guid LienId { get; init; }
    public string LienNumber { get; init; } = string.Empty;
    public int PaymentNumber { get; init; }
    public DateOnly? PaymentDate { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
    public decimal Amount { get; init; }
    public string? DetailsContext { get; init; }
    public string? Notes { get; init; }
    public string? SettlementType { get; init; }
    public string? SettlementStatus { get; init; }
    public string PostingStatus { get; init; } = "Posted";
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RecordCasePaymentRequest
{
    public required decimal Amount { get; init; }
    public required DateOnly PaymentDate { get; init; }
    public string? PaymentMethod { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? DetailsContext { get; init; }
    public string? Notes { get; init; }
    public string? SettlementType { get; init; }
    public string? SettlementStatus { get; init; }
    public string? LienStatus { get; init; }
    public List<CasePaymentAllocationRequest> Allocations { get; init; } = [];
}

public sealed class CasePaymentAllocationRequest
{
    public Guid LienId { get; init; }
    public decimal Amount { get; init; }
}

public sealed class RecordCasePaymentResponse
{
    public Guid ReceiptId { get; init; }
    public int PaymentNumber { get; init; }
    public decimal Amount { get; init; }
    public List<CasePaymentItemResponse> Allocations { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VoidCasePaymentRequest
{
    public string? Reason { get; init; }
}
