namespace Liens.Application.DTOs;

public sealed class WeeklyAgingDetailReportResult
{
    public DateOnly AsOfDate { get; init; }
    public string Currency { get; init; } = "USD";
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public List<WeeklyAgingDetailReportRow> Items { get; init; } = [];
}

public sealed class WeeklyAgingDetailReportRow
{
    public string LienCode { get; init; } = string.Empty;
    public string FundingCompany { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int AgingBucket { get; init; }
}
