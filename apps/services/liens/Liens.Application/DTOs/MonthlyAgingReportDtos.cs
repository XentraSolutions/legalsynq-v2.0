namespace Liens.Application.DTOs;

public sealed class MonthlyAgingReportResult
{
    public DateOnly AsOfDate { get; init; }
    public string Currency { get; init; } = "USD";
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public MonthlyAgingReportTotals SummaryTotals { get; init; } = new();
    public List<MonthlyAgingReportRow> Items { get; init; } = [];
}

public sealed class MonthlyAgingReportTotals
{
    public int TotalLiens { get; init; }
    public decimal Days1To30 { get; init; }
    public decimal Days31To60 { get; init; }
    public decimal Days61To90 { get; init; }
    public decimal Days91To120 { get; init; }
    public decimal MoreThan120 { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed class MonthlyAgingReportRow
{
    public string LienCode { get; init; } = string.Empty;
    public string FundingCompany { get; init; } = string.Empty;
    public decimal Days1To30 { get; init; }
    public decimal Days31To60 { get; init; }
    public decimal Days61To90 { get; init; }
    public decimal Days91To120 { get; init; }
    public decimal MoreThan120 { get; init; }
    public decimal TotalAmount { get; init; }
}
