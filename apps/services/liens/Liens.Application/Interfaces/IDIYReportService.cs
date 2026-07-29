using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IDIYReportService
{
    Task<List<DIYReportConfigResponse>> GetSavedReportsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<DIYReportConfigResponse>       GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<DIYReportConfigResponse>       SaveReportAsync(Guid tenantId, Guid userId, SaveDIYReportRequest request, CancellationToken ct = default);
    Task DeleteReportAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Execute a DIY report filter query and return paginated case rows.</summary>
    Task<DIYReportResult> RunReportAsync(Guid tenantId, DIYReportRunRequest request, CancellationToken ct = default);
}

public sealed class DIYReportRow
{
    public Guid?   CaseId       { get; init; }
    public Guid?   LienId       { get; init; }
    public string  CaseNumber   { get; init; } = string.Empty;
    public string  LienNumber   { get; init; } = string.Empty;
    public string  PlaintiffFirstName { get; init; } = string.Empty;
    public string  PlaintiffLastName  { get; init; } = string.Empty;
    public string  ClientName   { get; init; } = string.Empty;
    public string? Status       { get; init; }
    public string? CaseStatus   { get; init; }
    public DateOnly? DateOfLoss { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public DateTime? DateClosed { get; init; }
    public DateOnly? InitialServiceDate { get; init; }
    public decimal BillingAmount { get; init; }
    public decimal? PurchaseAmount { get; init; }
    public decimal? ReturnedAmount { get; init; }
    public decimal? LienTotal   { get; init; }
    public int NumberOfLiens { get; init; }
    public decimal? ToSettleAmount { get; init; }
    public decimal? SettledAmount { get; init; }
    public int? DaysSinceReductionApproval { get; init; }
    public string MedicalFacility { get; init; } = string.Empty;
    public string LawFirm { get; init; } = string.Empty;
    public string CaseType { get; init; } = string.Empty;
    public string CaseManager { get; init; } = string.Empty;
    public Dictionary<string, object?> Extra { get; init; } = new();
}

public sealed class DIYReportSummaryTotals
{
    public int TotalCases { get; init; }
    public int TotalLiens { get; init; }
    public decimal TotalPurchaseAmt { get; init; }
    public decimal TotalBillingAmt { get; init; }
    public decimal TotalAmtToSettle { get; init; }
    public decimal TotalReturnedAmt { get; init; }
    public decimal TotalGrossProfit { get; init; }
    public decimal AvgRoi { get; init; }
    public int TotalOpenCases { get; init; }
    public int TotalClosedCases { get; init; }
    public int TotalOpenLiens { get; init; }
    public int TotalClosedLiens { get; init; }
}

public sealed class DIYReportResult
{
    public string ReportType { get; init; } = "LIENS";
    public List<DIYReportRow> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public DIYReportSummaryTotals SummaryTotals { get; init; } = new();
}
