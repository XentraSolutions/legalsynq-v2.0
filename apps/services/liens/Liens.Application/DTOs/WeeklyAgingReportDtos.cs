namespace Liens.Application.DTOs;

public sealed class WeeklyAgingReportResult
{
    public DateOnly AsOfDate { get; init; }
    public string Currency { get; init; } = "USD";
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public WeeklyAgingReportTotals SummaryTotals { get; init; } = new();
    public List<WeeklyAgingReportRow> Items { get; init; } = [];
}

public sealed class WeeklyAgingReportTotals
{
    public int TotalLiens { get; init; }
    public decimal Days1To7 { get; init; }
    public decimal Days8To14 { get; init; }
    public decimal Days15To21 { get; init; }
    public decimal Days22To28 { get; init; }
    public decimal MoreThan28 { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed class WeeklyAgingReportRow
{
    public string LienCode { get; init; } = string.Empty;
    public string FundingCompany { get; init; } = string.Empty;
    public decimal Days1To7 { get; init; }
    public decimal Days8To14 { get; init; }
    public decimal Days15To21 { get; init; }
    public decimal Days22To28 { get; init; }
    public decimal MoreThan28 { get; init; }
    public decimal TotalAmount { get; init; }
}
