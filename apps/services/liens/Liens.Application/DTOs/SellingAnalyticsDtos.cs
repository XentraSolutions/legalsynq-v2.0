namespace Liens.Application.DTOs;

public sealed class SellingAnalyticsFilter
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public List<string> SellerStatuses { get; init; } = [];
    public List<string> ListingVisibilities { get; init; } = [];
    public List<Guid> FundingCompanyIds { get; init; } = [];
    public List<Guid> FacilityIds { get; init; } = [];
    public bool IncludeArchived { get; init; }
    public string DateDimension { get; init; } = "submitted";
    public string Grain { get; init; } = "month";
    public string? ConcentrationDimension { get; init; }
}

public sealed class SellingAnalyticsOverviewResponse
{
    public SellingAnalyticsSummary Summary { get; init; } = new();
}

public sealed class SellingAnalyticsSummary
{
    public int TotalCount { get; init; }
    public decimal PortfolioValue { get; init; }
    public decimal AskAmount { get; init; }
    public decimal HighestBidAmount { get; init; }
    public decimal SoldAmount { get; init; }
    public int DraftCount { get; init; }
    public int PendingCount { get; init; }
    public int InternalCount { get; init; }
    public int PreparedForSaleCount { get; init; }
    public int SubmittedForSaleCount { get; init; }
    public int SoldCount { get; init; }
    public int WithdrawnCount { get; init; }
    public int ArchivedCount { get; init; }
}

public sealed class SellingAnalyticsStatusBreakdownResponse
{
    public List<SellingAnalyticsStatusBreakdownItem> Items { get; init; } = [];
}

public sealed class SellingAnalyticsStatusBreakdownItem
{
    public string SellerStatus { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal OriginalAmountTotal { get; init; }
    public decimal AskAmountTotal { get; init; }
    public decimal HighestBidTotal { get; init; }
    public decimal PercentOfTotal { get; init; }
}

public sealed class SellingAnalyticsFunnelResponse
{
    public List<SellingAnalyticsFunnelStage> Stages { get; init; } = [];
}

public sealed class SellingAnalyticsFunnelStage
{
    public string Stage { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Value { get; init; }
    public decimal ConversionRate { get; init; }
    public decimal DropoffRate { get; init; }
}

public sealed class SellingAnalyticsTimeseriesResponse
{
    public string DateDimension { get; init; } = string.Empty;
    public string Grain { get; init; } = string.Empty;
    public List<SellingAnalyticsTimeseriesPoint> Points { get; init; } = [];
}

public sealed class SellingAnalyticsTimeseriesPoint
{
    public DateOnly BucketStart { get; init; }
    public int LienCount { get; init; }
    public decimal PortfolioValue { get; init; }
    public decimal AskAmount { get; init; }
    public decimal HighestBidAmount { get; init; }
    public decimal SoldAmount { get; init; }
    public int OfferCount { get; init; }
}

public sealed class SellingAnalyticsOffersResponse
{
    public int TotalOfferCount { get; init; }
    public int ActiveOfferCount { get; init; }
    public int AcceptedOfferCount { get; init; }
    public decimal HighestOfferAmount { get; init; }
    public decimal AverageOfferAmount { get; init; }
    public decimal AverageBidToAskRatio { get; init; }
    public decimal AcceptanceRate { get; init; }
}

public sealed class SellingAnalyticsBuyerPerformanceResponse
{
    public List<SellingAnalyticsBuyerPerformanceItem> Items { get; init; } = [];
}

public sealed class SellingAnalyticsBuyerPerformanceItem
{
    public Guid BuyerOrgId { get; init; }
    public int OfferCount { get; init; }
    public int AcceptedOfferCount { get; init; }
    public decimal SoldAmount { get; init; }
    public decimal HighestOfferAmount { get; init; }
    public decimal AverageOfferAmount { get; init; }
    public decimal AcceptanceRate { get; init; }
    public decimal AverageBidToAskRatio { get; init; }
}

public sealed class SellingAnalyticsAgingResponse
{
    public List<SellingAnalyticsAgingBucket> Buckets { get; init; } = [];
}

public sealed class SellingAnalyticsAgingBucket
{
    public string Bucket { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal PortfolioValue { get; init; }
}

public sealed class SellingAnalyticsConcentrationResponse
{
    public string Dimension { get; init; } = string.Empty;
    public List<SellingAnalyticsConcentrationItem> Items { get; init; } = [];
}

public sealed class SellingAnalyticsConcentrationItem
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal PortfolioValue { get; init; }
    public decimal PercentOfTotal { get; init; }
}

public sealed class SellingAnalyticsFilterOptionsResponse
{
    public List<SellingAnalyticsFilterOption> SellerStatuses { get; init; } = [];
    public List<SellingAnalyticsFilterOption> ListingVisibilities { get; init; } = [];
    public List<SellingAnalyticsFilterOption> FundingCompanies { get; init; } = [];
    public List<SellingAnalyticsFilterOption> Facilities { get; init; } = [];
}

public sealed class SellingAnalyticsFilterOption
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class SellingLienAnalyticsResponse
{
    public Guid LienId { get; init; }
    public string LienNumber { get; init; } = string.Empty;
    public string SellerStatus { get; init; } = string.Empty;
    public string LienStatus { get; init; } = string.Empty;
    public decimal OriginalAmount { get; init; }
    public decimal? AskAmount { get; init; }
    public decimal HighestBidAmount { get; init; }
    public decimal? PurchasePrice { get; init; }
    public DateTime? SubmittedForSaleAtUtc { get; init; }
    public DateTime? SoldAtUtc { get; init; }
    public int DaysInCurrentStatus { get; init; }
    public int OfferCount { get; init; }
    public int ActiveOfferCount { get; init; }
    public int AcceptedOfferCount { get; init; }
}

public sealed class SellingAnalyticsExportRequest
{
    public string Report { get; init; } = "overview";
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public List<string> SellerStatus { get; init; } = [];
    public List<string> ListingVisibility { get; init; } = [];
    public List<Guid> FundingCompanyId { get; init; } = [];
    public List<Guid> FacilityId { get; init; } = [];
    public bool IncludeArchived { get; init; }
    public string DateDimension { get; init; } = "submitted";
    public string Grain { get; init; } = "month";
    public string? ConcentrationDimension { get; init; }
}

public sealed class SellingAnalyticsExportResult
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
}
