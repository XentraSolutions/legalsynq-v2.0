namespace Liens.Application.DTOs;

public sealed class SellingOperationsDashboardQuery
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string Compare { get; init; } = "previousPeriod";
}

public sealed class SellingOperationsDashboardResponse
{
    public SellingOperationsDashboardPeriod Period { get; init; } = new();
    public SellingOperationsDashboardPeriod? ComparisonPeriod { get; init; }
    public string Currency { get; init; } = "USD";
    public SellingOperationsDashboardMetrics Metrics { get; init; } = new();
    public SellingOperationsArAgingResponse ArAging { get; init; } = new();
    public List<SellingOperationsStatusItem> LienStatuses { get; init; } = [];
    public List<SellingOperationsStatusItem> SellerStatuses { get; init; } = [];
    public List<SellingOperationsTimeseriesPoint> TimeSeries { get; init; } = [];
    public List<SellingOperationsTopBuyerItem> TopBuyers { get; init; } = [];
    public SellingOperationsBuyerAgingResponse BuyerAging { get; init; } = new();
    public DateTime GeneratedAtUtc { get; init; }
}

public sealed class SellingOperationsDashboardPeriod
{
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public string DateBasis { get; init; } = "initialServiceDate";
}

public sealed class SellingOperationsDashboardMetrics
{
    public SellingOperationsMetric TotalLienRevenue { get; init; } = new();
    public SellingOperationsMetric TotalOutstanding { get; init; } = new();
    public SellingOperationsMetric PastAmountDue { get; init; } = new();
    public SellingOperationsMetric Payments { get; init; } = new();
}

public sealed class SellingOperationsMetric
{
    public bool IsAvailable { get; init; }
    public decimal? Value { get; init; }
    public decimal? ComparisonValue { get; init; }
    public decimal? ChangeAmount { get; init; }
    public decimal? ChangePercent { get; init; }
    public string Formula { get; init; } = string.Empty;
    public string? UnavailableReason { get; init; }
}

public sealed class SellingOperationsArAgingResponse
{
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public decimal? Total { get; init; }
    public List<SellingOperationsAgingBucket> Buckets { get; init; } = [];
}

public sealed class SellingOperationsAgingBucket
{
    public string Bucket { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int LienCount { get; init; }
}

public sealed class SellingOperationsStatusItem
{
    public string Status { get; init; } = string.Empty;
    public int LienCount { get; init; }
    public decimal OriginalAmount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public decimal PercentOfLiens { get; init; }
}

public sealed class SellingOperationsTimeseriesPoint
{
    public DateOnly BucketStart { get; init; }
    public string Grain { get; init; } = "month";
    public int LienCount { get; init; }
    public decimal LienRevenue { get; init; }
    public decimal OutstandingAmount { get; init; }
}

public sealed class SellingOperationsTopBuyerItem
{
    public Guid BuyerOrgId { get; init; }
    public Guid? BuyerCompanyId { get; init; }
    public string BuyerName { get; init; } = string.Empty;
    public int ActiveLienCount { get; init; }
    public decimal TotalBalance { get; init; }
    public decimal CompletedPurchaseAmount { get; init; }
    public decimal PercentOfTotalBalance { get; init; }
}

public sealed class SellingOperationsBuyerAgingResponse
{
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public List<SellingOperationsBuyerAgingItem> Items { get; init; } = [];
}

public sealed class SellingOperationsBuyerAgingItem
{
    public Guid BuyerOrgId { get; init; }
    public Guid? BuyerCompanyId { get; init; }
    public string BuyerName { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public decimal? PastDuePercent { get; init; }
    public List<SellingOperationsAgingBucket> Buckets { get; init; } = [];
}
