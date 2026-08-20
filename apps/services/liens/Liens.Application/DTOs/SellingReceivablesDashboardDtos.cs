namespace Liens.Application.DTOs;

public sealed class SellingReceivablesDashboardRequest
{
    public DateOnly AsOfDate { get; init; }
    public int Months { get; init; } = 6;
    public int TopBuyerLimit { get; init; } = 5;
}

public sealed class SellingReceivablesDashboardResponse
{
    public DateOnly AsOfDate { get; init; }
    public string Currency { get; init; } = "USD";
    public ReceivablesDashboardSummary Summary { get; init; } = new();
    public ReceivablesAgingSummary AgingSummary { get; init; } = new();
    public ReceivablesStatusBreakdown StatusBreakdown { get; init; } = new();
    public List<ReceivablesTimePoint> LiensOverTime { get; init; } = [];
    public List<ReceivablesBuyerSummary> TopBuyers { get; init; } = [];
    public List<ReceivablesBuyerAging> BuyerAging { get; init; } = [];
    public ReceivablesDataQuality DataQuality { get; init; } = new();
}

public sealed class ReceivablesDashboardSummary
{
    public ReceivablesMetric TotalReceivables { get; init; } = new();
    public ReceivablesMetric TotalOutstanding { get; init; } = new();
    public ReceivablesMetric PastDueBalance { get; init; } = new();
    public ReceivablesMetric PaymentsReceived { get; init; } = new();
}

public sealed class ReceivablesMetric
{
    public decimal Amount { get; init; }
    public decimal? TrendPercent { get; init; }
    public bool TrendAvailable { get; init; }
}

public sealed class ReceivablesAgingSummary
{
    public decimal TotalOutstanding { get; init; }
    public List<ReceivablesAgingBucket> Buckets { get; init; } = [];
    public int UnagedLienCount { get; init; }
    public decimal UnagedBalance { get; init; }
}

public sealed class ReceivablesAgingBucket
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int LienCount { get; init; }
    public decimal Amount { get; init; }
    public decimal Percent { get; init; }
}

public sealed class ReceivablesStatusBreakdown
{
    public int TotalLiens { get; init; }
    public List<ReceivablesStatusItem> Items { get; init; } = [];
}

public sealed class ReceivablesStatusItem
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Percent { get; init; }
}

public sealed class ReceivablesTimePoint
{
    public DateOnly PeriodStart { get; init; }
    public decimal? OutstandingBalance { get; init; }
    public int? LienCount { get; init; }
    public bool DataAvailable { get; init; }
}

public sealed class ReceivablesBuyerSummary
{
    public Guid BuyerId { get; init; }
    public string BuyerName { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public int ActiveLienCount { get; init; }
    public decimal OutstandingBalance { get; init; }
    public decimal PercentOfOutstanding { get; init; }
}

public sealed class ReceivablesBuyerAging
{
    public Guid BuyerId { get; init; }
    public string BuyerName { get; init; } = string.Empty;
    public decimal OutstandingBalance { get; init; }
    public decimal PastDuePercent { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public ReceivablesBuyerAgingBuckets Buckets { get; init; } = new();
}

public sealed class ReceivablesBuyerAgingBuckets
{
    public decimal Days0To30 { get; init; }
    public decimal Days31To60 { get; init; }
    public decimal Days61To90 { get; init; }
    public decimal Days91To120 { get; init; }
    public decimal Days120Plus { get; init; }
    public decimal Unaged { get; init; }
}

public sealed class ReceivablesDataQuality
{
    public int MissingDueDateCount { get; init; }
    public int UnassignedBuyerCount { get; init; }
    public int UndatedPaymentCount { get; init; }
    public bool HistoricalSnapshotsAvailable { get; init; }
}
