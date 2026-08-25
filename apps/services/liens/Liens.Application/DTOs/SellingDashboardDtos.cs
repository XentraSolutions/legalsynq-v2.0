namespace Liens.Application.DTOs;

public sealed class SellingDashboardQuery
{
    public string Tab { get; init; } = "pending";
    public string? Search { get; init; }
    public Guid? FundingCompanyId { get; init; }
    public Guid? LawFirmId { get; init; }
    public Guid? CaseManagerId { get; init; }
    public Guid? FacilityId { get; init; }
    public DateOnly? InitialServiceDateFrom { get; init; }
    public DateOnly? InitialServiceDateTo { get; init; }
    public string SortBy { get; init; } = "initialServiceDate";
    public string SortDirection { get; init; } = "desc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class SellingDashboardResponse
{
    public SellingDashboardSummary Summary { get; init; } = new();
    public List<SellingDashboardLienItem> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class SellingLienListResponse
{
    public List<SellingDashboardLienItem> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class SellingDashboardSummary
{
    public decimal TotalPortfolioValue { get; init; }
    public decimal TotalPending { get; init; }
    public decimal TotalInternal { get; init; }
    public decimal TotalSold { get; init; }
    public int PendingCount { get; init; }
    public int InternalCount { get; init; }
    public int SoldCount { get; init; }
}

public sealed class SellingDashboardLienItem
{
    public Guid LienId { get; init; }
    public string LienNumber { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CaseId { get; init; }
    public string? CaseNumber { get; init; }
    public Guid? FundingCompanyId { get; init; }
    public string? FundingCompany { get; init; }
    public Guid? LawFirmId { get; init; }
    public string? LawFirm { get; init; }
    public Guid? CaseManagerId { get; init; }
    public string? CaseManager { get; init; }
    public Guid? FacilityId { get; init; }
    public string? Facility { get; init; }
    public DateOnly? InitialServiceDate { get; init; }
    public decimal BillingAmount { get; init; }
    public decimal? AskAmount { get; init; }
    public decimal HighestBidAmount { get; init; }
    public decimal? PurchasePrice { get; init; }
    public string Status { get; init; } = string.Empty;
}
