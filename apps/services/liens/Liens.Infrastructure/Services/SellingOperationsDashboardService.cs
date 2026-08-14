using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Services;

public sealed class SellingOperationsDashboardService : ISellingOperationsDashboardService
{
    private const string AgingUnavailableReason =
        "Lien receivables do not currently persist a due date, so past-due and A/R aging values cannot be calculated reliably.";

    private readonly LiensDbContext _db;
    private readonly TimeProvider _timeProvider;

    public SellingOperationsDashboardService(LiensDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<SellingOperationsDashboardResponse> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingOperationsDashboardQuery query,
        CancellationToken ct = default)
    {
        var generatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var period = ResolvePeriod(query, generatedAtUtc);
        DashboardPeriod? comparisonPeriod = string.Equals(query.Compare, "previousPeriod", StringComparison.Ordinal)
            ? PreviousPeriod(period)
            : null;

        var scopedLiens = ScopedLiens(tenantId, sellerOrgId);
        var currentFinancials = await GetFinancialsAsync(scopedLiens, period, ct);
        var comparisonFinancials = comparisonPeriod.HasValue
            ? await GetFinancialsAsync(scopedLiens, comparisonPeriod.Value, ct)
            : null;
        var currentPayments = await GetPaymentsAsync(scopedLiens, tenantId, period, ct);
        var comparisonPayments = comparisonPeriod.HasValue
            ? await GetPaymentsAsync(scopedLiens, tenantId, comparisonPeriod.Value, ct)
            : (decimal?)null;

        var lienStatuses = await GetOperationalStatusesAsync(scopedLiens, period, ct);
        var sellerStatuses = await GetSellerStatusesAsync(scopedLiens, period, ct);
        var timeSeries = await GetTimeSeriesAsync(scopedLiens, period, ct);
        var topBuyers = await GetTopBuyersAsync(scopedLiens, tenantId, sellerOrgId, period, ct);

        return new SellingOperationsDashboardResponse
        {
            Period = ToResponse(period),
            ComparisonPeriod = comparisonPeriod.HasValue ? ToResponse(comparisonPeriod.Value) : null,
            Currency = "USD",
            Metrics = new SellingOperationsDashboardMetrics
            {
                TotalLienRevenue = AvailableMetric(
                    currentFinancials.LienRevenue,
                    comparisonFinancials?.LienRevenue,
                    "Sum of OriginalAmount for seller-scoped, non-archived liens whose InitialServiceDate is within the period."),
                TotalOutstanding = AvailableMetric(
                    currentFinancials.Outstanding,
                    comparisonFinancials?.Outstanding,
                    "Sum of CurrentBalance, falling back to OriginalAmount when CurrentBalance is null, for the period lien cohort."),
                PastAmountDue = new SellingOperationsMetric
                {
                    IsAvailable = false,
                    Formula = "Unavailable until an authoritative receivable due date is persisted.",
                    UnavailableReason = AgingUnavailableReason,
                },
                Payments = AvailableMetric(
                    currentPayments,
                    comparisonPayments,
                    "Sum of non-deleted SettlementPaymentDetail.Amount values whose PaymentDate is within the period and whose lien belongs to the seller."),
            },
            ArAging = new SellingOperationsArAgingResponse
            {
                IsAvailable = false,
                UnavailableReason = AgingUnavailableReason,
                Total = null,
            },
            LienStatuses = lienStatuses,
            SellerStatuses = sellerStatuses,
            TimeSeries = timeSeries,
            TopBuyers = topBuyers,
            BuyerAging = new SellingOperationsBuyerAgingResponse
            {
                IsAvailable = false,
                UnavailableReason = AgingUnavailableReason,
            },
            GeneratedAtUtc = generatedAtUtc,
        };
    }

    private IQueryable<Lien> ScopedLiens(Guid tenantId, Guid sellerOrgId)
    {
        return _db.Liens.AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && (l.SellingOrgId == sellerOrgId
                    || (!l.SellingOrgId.HasValue && l.OrgId == sellerOrgId))
                && l.ArchivedAtUtc == null
                && (l.SellerStatus == null || l.SellerStatus != SellingLienStatus.Archived));
    }

    private static IQueryable<Lien> InPeriod(IQueryable<Lien> query, DashboardPeriod period)
    {
        return query.Where(l => l.InitialServiceDate.HasValue
            && l.InitialServiceDate.Value >= period.DateFrom
            && l.InitialServiceDate.Value <= period.DateTo);
    }

    private static async Task<FinancialAggregate> GetFinancialsAsync(
        IQueryable<Lien> scopedLiens,
        DashboardPeriod period,
        CancellationToken ct)
    {
        var row = await InPeriod(scopedLiens, period)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                LienRevenue = group.Sum(l => l.OriginalAmount),
                Outstanding = group.Sum(l => l.CurrentBalance ?? l.OriginalAmount),
            })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? new FinancialAggregate(0m, 0m)
            : new FinancialAggregate(row.LienRevenue, row.Outstanding);
    }

    private async Task<decimal> GetPaymentsAsync(
        IQueryable<Lien> scopedLiens,
        Guid tenantId,
        DashboardPeriod period,
        CancellationToken ct)
    {
        var row = await _db.SettlementPaymentDetails.AsNoTracking()
            .Where(payment => payment.TenantId == tenantId
                && !payment.IsDeleted
                && payment.PaymentDate.HasValue
                && payment.PaymentDate.Value >= period.DateFrom
                && payment.PaymentDate.Value <= period.DateTo)
            .Join(scopedLiens, payment => payment.LienId, lien => lien.Id, (payment, _) => payment)
            .GroupBy(_ => 1)
            .Select(group => new { Amount = group.Sum(payment => payment.Amount) })
            .FirstOrDefaultAsync(ct);

        return row?.Amount ?? 0m;
    }

    private static async Task<List<SellingOperationsStatusItem>> GetOperationalStatusesAsync(
        IQueryable<Lien> scopedLiens,
        DashboardPeriod period,
        CancellationToken ct)
    {
        var groups = await InPeriod(scopedLiens, period)
            .GroupBy(l => l.Status)
            .Select(group => new
            {
                Status = group.Key,
                LienCount = group.Count(),
                OriginalAmount = group.Sum(l => l.OriginalAmount),
                OutstandingAmount = group.Sum(l => l.CurrentBalance ?? l.OriginalAmount),
            })
            .OrderBy(group => group.Status)
            .ToListAsync(ct);
        var totalCount = groups.Sum(item => item.LienCount);

        return groups.Select(item => ToStatusItem(
            item.Status,
            item.LienCount,
            item.OriginalAmount,
            item.OutstandingAmount,
            totalCount)).ToList();
    }

    private static async Task<List<SellingOperationsStatusItem>> GetSellerStatusesAsync(
        IQueryable<Lien> scopedLiens,
        DashboardPeriod period,
        CancellationToken ct)
    {
        var groups = await InPeriod(scopedLiens, period)
            .GroupBy(l => new
            {
                l.SellerStatus,
                l.Status,
                HasSoldAt = l.SoldAtUtc != null,
                HasPurchasePrice = l.PurchasePrice != null,
            })
            .Select(group => new
            {
                group.Key.SellerStatus,
                LienStatus = group.Key.Status,
                group.Key.HasSoldAt,
                group.Key.HasPurchasePrice,
                LienCount = group.Count(),
                OriginalAmount = group.Sum(l => l.OriginalAmount),
                OutstandingAmount = group.Sum(l => l.CurrentBalance ?? l.OriginalAmount),
            })
            .ToListAsync(ct);

        var combined = groups
            .GroupBy(group => EffectiveSellerStatus(
                group.SellerStatus,
                group.LienStatus,
                group.HasSoldAt,
                group.HasPurchasePrice))
            .Select(group => new
            {
                Status = group.Key,
                LienCount = group.Sum(item => item.LienCount),
                OriginalAmount = group.Sum(item => item.OriginalAmount),
                OutstandingAmount = group.Sum(item => item.OutstandingAmount),
            })
            .OrderBy(group => StatusOrder(group.Status))
            .ThenBy(group => group.Status, StringComparer.Ordinal)
            .ToList();
        var totalCount = combined.Sum(item => item.LienCount);

        return combined.Select(item => ToStatusItem(
            item.Status,
            item.LienCount,
            item.OriginalAmount,
            item.OutstandingAmount,
            totalCount)).ToList();
    }

    private static async Task<List<SellingOperationsTimeseriesPoint>> GetTimeSeriesAsync(
        IQueryable<Lien> scopedLiens,
        DashboardPeriod period,
        CancellationToken ct)
    {
        var dailyGroups = await InPeriod(scopedLiens, period)
            .GroupBy(l => l.InitialServiceDate!.Value)
            .Select(group => new
            {
                Date = group.Key,
                LienCount = group.Count(),
                LienRevenue = group.Sum(l => l.OriginalAmount),
                OutstandingAmount = group.Sum(l => l.CurrentBalance ?? l.OriginalAmount),
            })
            .ToListAsync(ct);

        return dailyGroups
            .GroupBy(item => new DateOnly(item.Date.Year, item.Date.Month, 1))
            .OrderBy(group => group.Key)
            .Select(group => new SellingOperationsTimeseriesPoint
            {
                BucketStart = group.Key,
                Grain = "month",
                LienCount = group.Sum(item => item.LienCount),
                LienRevenue = group.Sum(item => item.LienRevenue),
                OutstandingAmount = group.Sum(item => item.OutstandingAmount),
            })
            .ToList();
    }

    private async Task<List<SellingOperationsTopBuyerItem>> GetTopBuyersAsync(
        IQueryable<Lien> scopedLiens,
        Guid tenantId,
        Guid sellerOrgId,
        DashboardPeriod period,
        CancellationToken ct)
    {
        var buyerBalanceLiens = InPeriod(scopedLiens, period)
            .Where(l => l.BuyingOrgId.HasValue
                && l.Status != LienStatus.Settled
                && l.Status != LienStatus.Cancelled
                && l.Status != LienStatus.Declined
                && l.Status != LienStatus.Withdrawn
                && (l.CurrentBalance ?? l.OriginalAmount) > 0m);
        var totalBalanceRow = await buyerBalanceLiens
            .GroupBy(_ => 1)
            .Select(group => new { TotalBalance = group.Sum(l => l.CurrentBalance ?? l.OriginalAmount) })
            .FirstOrDefaultAsync(ct);
        var buyers = await buyerBalanceLiens
            .GroupBy(l => l.BuyingOrgId!.Value)
            .Select(group => new
            {
                BuyerOrgId = group.Key,
                ActiveLienCount = group.Count(),
                TotalBalance = group.Sum(l => l.CurrentBalance ?? l.OriginalAmount),
            })
            .OrderByDescending(item => item.TotalBalance)
            .ThenBy(item => item.BuyerOrgId)
            .Take(5)
            .ToListAsync(ct);

        if (buyers.Count == 0)
            return [];

        var buyerOrgIds = buyers.Select(item => item.BuyerOrgId).ToList();
        var completedPurchases = await InPeriod(scopedLiens, period)
            .Where(l => l.BuyingOrgId.HasValue
                && buyerOrgIds.Contains(l.BuyingOrgId.Value)
                && (l.SellerStatus == SellingLienStatus.Sold
                    || l.Status == LienStatus.Sold
                    || l.Status == LienStatus.Active
                    || l.Status == LienStatus.Settled
                    || l.Status == LienStatus.Disputed)
                && l.SoldAtUtc != null
                && l.PurchasePrice.HasValue)
            .GroupBy(l => l.BuyingOrgId!.Value)
            .Select(group => new
            {
                BuyerOrgId = group.Key,
                Amount = group.Sum(l => l.PurchasePrice!.Value),
            })
            .ToDictionaryAsync(item => item.BuyerOrgId, item => item.Amount, ct);
        var companyLinks = await _db.LienOffers.AsNoTracking()
            .Where(offer => offer.TenantId == tenantId
                && offer.SellerOrgId == sellerOrgId
                && buyerOrgIds.Contains(offer.BuyerOrgId)
                && offer.BuyerCompanyId.HasValue
                && offer.Status == OfferStatus.Accepted)
            .Join(
                InPeriod(scopedLiens, period),
                offer => offer.LienId,
                lien => lien.Id,
                (offer, _) => new
                {
                    offer.Id,
                    offer.BuyerOrgId,
                    BuyerCompanyId = offer.BuyerCompanyId!.Value,
                    offer.RespondedAtUtc,
                    offer.OfferedAtUtc,
                })
            .OrderBy(link => link.BuyerOrgId)
            .ThenByDescending(link => link.RespondedAtUtc)
            .ThenByDescending(link => link.OfferedAtUtc)
            .ThenBy(link => link.Id)
            .ToListAsync(ct);
        var companyByBuyerOrg = companyLinks
            .GroupBy(link => link.BuyerOrgId)
            .ToDictionary(group => group.Key, group => (Guid?)group.First().BuyerCompanyId);
        var companyIds = companyByBuyerOrg.Values.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var companyNames = companyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Companies.AsNoTracking()
                .Where(company => company.TenantId == tenantId
                    && company.OrgId == sellerOrgId
                    && companyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id, company => company.Name, ct);
        var totalBalance = totalBalanceRow?.TotalBalance ?? 0m;

        return buyers.Select(item =>
        {
            var companyId = companyByBuyerOrg.GetValueOrDefault(item.BuyerOrgId);
            return new SellingOperationsTopBuyerItem
            {
                BuyerOrgId = item.BuyerOrgId,
                BuyerCompanyId = companyId,
                BuyerName = companyId.HasValue && companyNames.TryGetValue(companyId.Value, out var name)
                    ? name
                    : item.BuyerOrgId.ToString(),
                ActiveLienCount = item.ActiveLienCount,
                TotalBalance = item.TotalBalance,
                CompletedPurchaseAmount = completedPurchases.GetValueOrDefault(item.BuyerOrgId),
                PercentOfTotalBalance = totalBalance == 0m
                    ? 0m
                    : decimal.Round(item.TotalBalance * 100m / totalBalance, 2),
            };
        }).ToList();
    }

    private static SellingOperationsStatusItem ToStatusItem(
        string status,
        int lienCount,
        decimal originalAmount,
        decimal outstandingAmount,
        int totalCount) => new()
    {
        Status = status,
        LienCount = lienCount,
        OriginalAmount = originalAmount,
        OutstandingAmount = outstandingAmount,
        PercentOfLiens = totalCount == 0
            ? 0m
            : decimal.Round(lienCount * 100m / totalCount, 2),
    };

    private static SellingOperationsMetric AvailableMetric(
        decimal value,
        decimal? comparisonValue,
        string formula)
    {
        decimal? changeAmount = comparisonValue.HasValue ? value - comparisonValue.Value : null;
        decimal? changePercent = comparisonValue is > 0m
            ? decimal.Round((value - comparisonValue.Value) * 100m / comparisonValue.Value, 2)
            : null;

        return new SellingOperationsMetric
        {
            IsAvailable = true,
            Value = value,
            ComparisonValue = comparisonValue,
            ChangeAmount = changeAmount,
            ChangePercent = changePercent,
            Formula = formula,
        };
    }

    private static string EffectiveSellerStatus(
        string? sellerStatus,
        string lienStatus,
        bool hasSoldAt,
        bool hasPurchasePrice)
    {
        var indicatesCompletedLifecycle = sellerStatus == SellingLienStatus.Sold
            || lienStatus is LienStatus.Sold or LienStatus.Active or LienStatus.Settled or LienStatus.Disputed;
        if (indicatesCompletedLifecycle && hasSoldAt && hasPurchasePrice)
            return SellingLienStatus.Sold;

        if (sellerStatus == SellingLienStatus.Sold || lienStatus == LienStatus.Sold)
            return "SaleIncomplete";

        if (!string.IsNullOrWhiteSpace(sellerStatus))
            return sellerStatus;

        return lienStatus switch
        {
            LienStatus.Accepted => SellingLienStatus.Accepted,
            LienStatus.Declined => SellingLienStatus.Declined,
            LienStatus.Withdrawn => SellingLienStatus.Withdrawn,
            LienStatus.Offered or LienStatus.UnderReview => SellingLienStatus.SubmittedForSale,
            _ => SellingLienStatus.Pending,
        };
    }

    private static int StatusOrder(string status) => status switch
    {
        SellingLienStatus.Pending => 0,
        SellingLienStatus.Internal => 1,
        SellingLienStatus.PreparedForSale => 2,
        SellingLienStatus.SubmittedForSale => 3,
        SellingLienStatus.Accepted => 4,
        SellingLienStatus.Sold => 5,
        "SaleIncomplete" => 6,
        SellingLienStatus.Declined => 7,
        SellingLienStatus.Withdrawn => 8,
        _ => 9,
    };

    private static DashboardPeriod ResolvePeriod(
        SellingOperationsDashboardQuery query,
        DateTime generatedAtUtc)
    {
        if (query.DateFrom.HasValue && query.DateTo.HasValue)
            return new DashboardPeriod(query.DateFrom.Value, query.DateTo.Value);

        var today = DateOnly.FromDateTime(generatedAtUtc);
        return new DashboardPeriod(new DateOnly(today.Year, today.Month, 1), today);
    }

    private static DashboardPeriod PreviousPeriod(DashboardPeriod period)
    {
        var inclusiveDays = period.DateTo.DayNumber - period.DateFrom.DayNumber + 1;
        var previousFromDayNumber = period.DateFrom.DayNumber - inclusiveDays;
        if (previousFromDayNumber < DateOnly.MinValue.DayNumber)
        {
            throw new ValidationException(
                "Selling operations dashboard query is invalid.",
                new Dictionary<string, string[]>
                {
                    ["compare"] = ["The previous comparison period would be before the minimum supported date."],
                });
        }

        return new DashboardPeriod(
            DateOnly.FromDayNumber(previousFromDayNumber),
            DateOnly.FromDayNumber(period.DateFrom.DayNumber - 1));
    }

    private static SellingOperationsDashboardPeriod ToResponse(DashboardPeriod period) => new()
    {
        DateFrom = period.DateFrom,
        DateTo = period.DateTo,
        DateBasis = "initialServiceDate",
    };

    private readonly record struct DashboardPeriod(DateOnly DateFrom, DateOnly DateTo);
    private sealed record FinancialAggregate(decimal LienRevenue, decimal Outstanding);
}
