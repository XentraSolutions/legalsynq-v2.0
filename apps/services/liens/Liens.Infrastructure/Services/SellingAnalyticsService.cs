using System.Globalization;
using System.Text;
using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Services;

public sealed class SellingAnalyticsService : ISellingAnalyticsService
{
    private const int ExportRowLimit = 10_000;
    private readonly LiensDbContext _db;

    public SellingAnalyticsService(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<SellingAnalyticsOverviewResponse> GetOverviewAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        return new SellingAnalyticsOverviewResponse { Summary = BuildSummary(data.Liens, data.Offers) };
    }

    public async Task<SellingAnalyticsStatusBreakdownResponse> GetStatusBreakdownAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        var total = data.Liens.Count;
        var items = SellingLienStatus.All
            .Select(status =>
            {
                var liens = data.Liens.Where(l => StatusFor(l) == status).ToList();
                return new SellingAnalyticsStatusBreakdownItem
                {
                    SellerStatus = status,
                    Count = liens.Count,
                    OriginalAmountTotal = liens.Sum(l => l.OriginalAmount),
                    AskAmountTotal = liens.Sum(l => l.AskAmount ?? 0m),
                    HighestBidTotal = liens.Sum(l => HighestBidFor(l.Id, l.HighestBidAmount, data.Offers)),
                    PercentOfTotal = Percentage(liens.Count, total),
                };
            })
            .Where(i => i.Count > 0)
            .ToList();

        return new SellingAnalyticsStatusBreakdownResponse { Items = items };
    }

    public async Task<SellingAnalyticsFunnelResponse> GetFunnelAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        var submitted = data.Liens.Where(l => StatusFor(l) == SellingLienStatus.SubmittedForSale).ToList();
        var sold = data.Liens.Where(IsSoldForAnalytics).ToList();
        var stageData = new (string Stage, List<Lien> Liens)[]
        {
            ("Pending/Internal", data.Liens.Where(l => StatusFor(l) is SellingLienStatus.Pending or SellingLienStatus.Internal).ToList()),
            ("PreparedForSale", data.Liens.Where(l => StatusFor(l) == SellingLienStatus.PreparedForSale).ToList()),
            ("SubmittedForSale", submitted),
            ("OfferReceived", data.Liens.Where(l => data.Offers.Any(o => o.LienId == l.Id)).ToList()),
            ("Sold", sold),
        };

        var previousCount = stageData.Length == 0 ? 0 : stageData[0].Liens.Count;
        var stages = new List<SellingAnalyticsFunnelStage>();

        foreach (var (stage, liens) in stageData)
        {
            var conversion = stages.Count == 0 ? 100m : Percentage(liens.Count, previousCount);
            stages.Add(new SellingAnalyticsFunnelStage
            {
                Stage = stage,
                Count = liens.Count,
                Value = liens.Sum(l => l.OriginalAmount),
                ConversionRate = conversion,
                DropoffRate = stages.Count == 0 ? 0m : Math.Max(0m, 100m - conversion),
            });
            previousCount = liens.Count;
        }

        return new SellingAnalyticsFunnelResponse { Stages = stages };
    }

    public async Task<SellingAnalyticsTimeseriesResponse> GetTimeseriesAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        if (filter.DateDimension == "offer")
        {
            var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: false, ct);
            var offers = FilterOffersByDate(data.Offers, filter).ToList();
            var points = offers
                .GroupBy(o => BucketStart(DateOnly.FromDateTime(o.OfferedAtUtc), filter.Grain))
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var bucketLienIds = g.Select(o => o.LienId).Distinct().ToHashSet();
                    var liens = data.Liens.Where(l => bucketLienIds.Contains(l.Id)).ToList();
                    return BuildTimeseriesPoint(g.Key, liens, g.ToList(), data.Offers);
                })
                .ToList();

            return new SellingAnalyticsTimeseriesResponse
            {
                DateDimension = filter.DateDimension,
                Grain = filter.Grain,
                Points = points,
            };
        }

        var lienData = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        var lienPoints = lienData.Liens
            .Select(l => new { Lien = l, Anchor = DateAnchorFor(l, filter.DateDimension) })
            .Where(x => x.Anchor.HasValue)
            .GroupBy(x => BucketStart(DateOnly.FromDateTime(x.Anchor!.Value), filter.Grain))
            .OrderBy(g => g.Key)
            .Select(g => BuildTimeseriesPoint(
                g.Key,
                g.Select(x => x.Lien).ToList(),
                lienData.Offers.Where(o => g.Any(x => x.Lien.Id == o.LienId)).ToList(),
                lienData.Offers))
            .ToList();

        return new SellingAnalyticsTimeseriesResponse
        {
            DateDimension = filter.DateDimension,
            Grain = filter.Grain,
            Points = lienPoints,
        };
    }

    public async Task<SellingAnalyticsOffersResponse> GetOffersAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: false, ct);
        var offers = FilterOffersByDate(data.Offers, filter).ToList();
        var accepted = offers.Count(o => o.Status == OfferStatus.Accepted);
        var askByLien = data.Liens.ToDictionary(l => l.Id, l => l.AskAmount ?? l.OfferPrice ?? 0m);
        var bidAskRatios = offers
            .Where(o => askByLien.TryGetValue(o.LienId, out var ask) && ask > 0)
            .Select(o => o.OfferAmount / askByLien[o.LienId])
            .ToList();

        return new SellingAnalyticsOffersResponse
        {
            TotalOfferCount = offers.Count,
            ActiveOfferCount = offers.Count(IsEligibleOffer),
            AcceptedOfferCount = accepted,
            HighestOfferAmount = offers.Count == 0 ? 0m : offers.Max(o => o.OfferAmount),
            AverageOfferAmount = offers.Count == 0 ? 0m : decimal.Round(offers.Average(o => o.OfferAmount), 2),
            AverageBidToAskRatio = bidAskRatios.Count == 0 ? 0m : decimal.Round(bidAskRatios.Average(), 4),
            AcceptanceRate = Percentage(accepted, offers.Count),
        };
    }

    public async Task<SellingAnalyticsBuyerPerformanceResponse> GetBuyerPerformanceAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: false, ct);
        var offers = FilterOffersByDate(data.Offers, filter).ToList();
        var liensById = data.Liens.ToDictionary(l => l.Id);

        var items = offers
            .GroupBy(o => o.BuyerOrgId)
            .Select(g =>
            {
                var groupOffers = g.ToList();
                var acceptedOffers = groupOffers.Where(o => o.Status == OfferStatus.Accepted).ToList();
                var askRatios = groupOffers
                    .Where(o => liensById.TryGetValue(o.LienId, out var lien) && (lien.AskAmount ?? lien.OfferPrice ?? 0m) > 0)
                    .Select(o =>
                    {
                        var lien = liensById[o.LienId];
                        var ask = lien.AskAmount ?? lien.OfferPrice ?? 0m;
                        return o.OfferAmount / ask;
                    })
                    .ToList();
                return new SellingAnalyticsBuyerPerformanceItem
                {
                    BuyerOrgId = g.Key,
                    OfferCount = groupOffers.Count,
                    AcceptedOfferCount = acceptedOffers.Count,
                    SoldAmount = acceptedOffers
                        .Where(o => liensById.TryGetValue(o.LienId, out var lien) && IsSoldForAnalytics(lien))
                        .Sum(o => liensById[o.LienId].PurchasePrice ?? 0m),
                    HighestOfferAmount = groupOffers.Max(o => o.OfferAmount),
                    AverageOfferAmount = decimal.Round(groupOffers.Average(o => o.OfferAmount), 2),
                    AcceptanceRate = Percentage(acceptedOffers.Count, groupOffers.Count),
                    AverageBidToAskRatio = askRatios.Count == 0 ? 0m : decimal.Round(askRatios.Average(), 4),
                };
            })
            .OrderByDescending(i => i.SoldAmount)
            .ThenByDescending(i => i.AcceptedOfferCount)
            .ThenByDescending(i => i.OfferCount)
            .Take(50)
            .ToList();

        return new SellingAnalyticsBuyerPerformanceResponse { Items = items };
    }

    public async Task<SellingAnalyticsAgingResponse> GetAgingAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        var now = DateTime.UtcNow;
        var buckets = new[]
        {
            new AgingBucketDefinition("0-7", 0, 7),
            new AgingBucketDefinition("8-14", 8, 14),
            new AgingBucketDefinition("15-30", 15, 30),
            new AgingBucketDefinition("31-60", 31, 60),
            new AgingBucketDefinition("60+", 61, int.MaxValue),
        };

        var items = buckets.Select(bucket =>
        {
            var liens = data.Liens
                .Where(l =>
                {
                    var days = Math.Max(0, (int)Math.Floor((now - StatusAnchorFor(l)).TotalDays));
                    return days >= bucket.MinDays && days <= bucket.MaxDays;
                })
                .ToList();

            return new SellingAnalyticsAgingBucket
            {
                Bucket = bucket.Label,
                Count = liens.Count,
                PortfolioValue = liens.Sum(l => l.OriginalAmount),
            };
        }).ToList();

        return new SellingAnalyticsAgingResponse { Buckets = items };
    }

    public async Task<SellingAnalyticsConcentrationResponse> GetConcentrationAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        string dimension,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        var total = data.Liens.Count;

        var groups = data.Liens
            .GroupBy(l => ConcentrationValue(l, dimension))
            .Select(g => new SellingAnalyticsConcentrationItem
            {
                Value = g.Key.Value,
                Label = g.Key.Label,
                Count = g.Count(),
                PortfolioValue = g.Sum(l => l.OriginalAmount),
                PercentOfTotal = Percentage(g.Count(), total),
            })
            .OrderByDescending(i => i.Count)
            .ThenBy(i => i.Label)
            .Take(50)
            .ToList();

        return new SellingAnalyticsConcentrationResponse
        {
            Dimension = dimension,
            Items = groups,
        };
    }

    public async Task<SellingAnalyticsFilterOptionsResponse> GetFilterOptionsAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        var fundingIds = data.Liens
            .Select(EffectiveFundingCompanyId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var facilityIds = data.Liens.Where(l => l.FacilityId.HasValue).Select(l => l.FacilityId!.Value).Distinct().ToList();
        var contactNames = await _db.Contacts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && fundingIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, ct);
        var companyNames = await _db.Companies.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.OrgId == sellerOrgId && fundingIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var facilityNames = await _db.Facilities.AsNoTracking()
            .Where(f => f.TenantId == tenantId && facilityIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        return new SellingAnalyticsFilterOptionsResponse
        {
            SellerStatuses = data.Liens
                .GroupBy(StatusFor)
                .OrderBy(g => g.Key)
                .Select(g => new SellingAnalyticsFilterOption { Value = g.Key, Label = g.Key, Count = g.Count() })
                .ToList(),
            ListingVisibilities = data.Liens
                .GroupBy(l => l.ListingVisibility ?? SellingListingVisibility.Private)
                .OrderBy(g => g.Key)
                .Select(g => new SellingAnalyticsFilterOption { Value = g.Key, Label = g.Key, Count = g.Count() })
                .ToList(),
            FundingCompanies = data.Liens
                .Select(l => new { Lien = l, FundingCompanyId = EffectiveFundingCompanyId(l) })
                .Where(item => item.FundingCompanyId.HasValue)
                .GroupBy(item => item.FundingCompanyId!.Value)
                .OrderBy(g => companyNames.TryGetValue(g.Key, out var companyName)
                    ? companyName
                    : contactNames.TryGetValue(g.Key, out var contactName) ? contactName : g.Key.ToString())
                .Select(g => new SellingAnalyticsFilterOption
                {
                    Value = g.Key.ToString(),
                    Label = companyNames.TryGetValue(g.Key, out var companyName)
                        ? companyName
                        : contactNames.TryGetValue(g.Key, out var contactName) ? contactName : g.Key.ToString(),
                    Count = g.Count(),
                })
                .ToList(),
            Facilities = data.Liens
                .Where(l => l.FacilityId.HasValue)
                .GroupBy(l => l.FacilityId!.Value)
                .OrderBy(g => facilityNames.TryGetValue(g.Key, out var name) ? name : g.Key.ToString())
                .Select(g => new SellingAnalyticsFilterOption
                {
                    Value = g.Key.ToString(),
                    Label = facilityNames.TryGetValue(g.Key, out var name) ? name : g.Key.ToString(),
                    Count = g.Count(),
                })
                .ToList(),
        };
    }

    public async Task<SellingLienAnalyticsResponse> GetLienAnalyticsAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid lienId,
        CancellationToken ct = default)
    {
        var lien = await _db.Liens.AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && l.Id == lienId
                && (l.SellingOrgId == sellerOrgId
                    || (!l.SellingOrgId.HasValue && l.OrgId == sellerOrgId)))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Selling lien '{lienId}' was not found for the current seller organization.");

        var offers = await _db.LienOffers.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.SellerOrgId == sellerOrgId && o.LienId == lienId)
            .ToListAsync(ct);

        return new SellingLienAnalyticsResponse
        {
            LienId = lien.Id,
            LienNumber = lien.LienNumber,
            SellerStatus = StatusFor(lien),
            LienStatus = lien.Status,
            OriginalAmount = lien.OriginalAmount,
            AskAmount = lien.AskAmount,
            HighestBidAmount = HighestBidFor(lien.Id, lien.HighestBidAmount, offers),
            PurchasePrice = lien.PurchasePrice,
            SubmittedForSaleAtUtc = lien.SubmittedForSaleAtUtc,
            SoldAtUtc = lien.SoldAtUtc,
            DaysInCurrentStatus = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - StatusAnchorFor(lien)).TotalDays)),
            OfferCount = offers.Count,
            ActiveOfferCount = offers.Count(IsEligibleOffer),
            AcceptedOfferCount = offers.Count(o => o.Status == OfferStatus.Accepted),
        };
    }

    public async Task<SellingAnalyticsExportResult> ExportAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsExportRequest request,
        CancellationToken ct = default)
    {
        var filter = new SellingAnalyticsFilter
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            SellerStatuses = request.SellerStatus,
            ListingVisibilities = request.ListingVisibility,
            FundingCompanyIds = request.FundingCompanyId,
            FacilityIds = request.FacilityId,
            IncludeArchived = request.IncludeArchived,
            DateDimension = request.DateDimension,
            Grain = request.Grain,
            ConcentrationDimension = request.ConcentrationDimension,
        };

        var data = await LoadDataAsync(tenantId, sellerOrgId, filter, applyLienDateFilter: true, ct);
        if (data.Liens.Count > ExportRowLimit)
        {
            throw new ValidationException("Selling analytics export exceeds the v1 row limit.",
                new Dictionary<string, string[]> { ["rowLimit"] = [$"Export is limited to {ExportRowLimit:N0} rows. Narrow the filters and retry."] });
        }

        var csv = BuildExportCsv(data);
        return new SellingAnalyticsExportResult
        {
            FileName = $"selling-analytics-{request.Report}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            ContentType = "text/csv",
            Content = Encoding.UTF8.GetBytes(csv),
        };
    }

    private async Task<AnalyticsData> LoadDataAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        bool applyLienDateFilter,
        CancellationToken ct)
    {
        var query = _db.Liens.AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && (l.SellingOrgId == sellerOrgId
                    || (!l.SellingOrgId.HasValue && l.OrgId == sellerOrgId)));

        if (!filter.IncludeArchived)
            query = query.Where(l => (l.SellerStatus == null || l.SellerStatus != SellingLienStatus.Archived) && l.ArchivedAtUtc == null);

        if (filter.ListingVisibilities.Count > 0)
            query = query.Where(l => l.ListingVisibility != null && filter.ListingVisibilities.Contains(l.ListingVisibility));

        if (filter.FundingCompanyIds.Count > 0)
            query = query.Where(l =>
                (l.FundingCompanyCompanyId.HasValue
                    && filter.FundingCompanyIds.Contains(l.FundingCompanyCompanyId.Value))
                || (!l.FundingCompanyCompanyId.HasValue
                    && l.FundingCompanyId.HasValue
                    && filter.FundingCompanyIds.Contains(l.FundingCompanyId.Value)));

        if (filter.FacilityIds.Count > 0)
            query = query.Where(l => l.FacilityId.HasValue && filter.FacilityIds.Contains(l.FacilityId.Value));

        var liens = await query.ToListAsync(ct);

        if (filter.SellerStatuses.Count > 0)
            liens = liens.Where(l => filter.SellerStatuses.Contains(StatusFor(l))).ToList();

        if (applyLienDateFilter && (filter.StartDate.HasValue || filter.EndDate.HasValue))
            liens = liens.Where(l => DateWithin(DateAnchorFor(l, filter.DateDimension), filter)).ToList();

        var lienIds = liens.Select(l => l.Id).ToList();
        var offers = lienIds.Count == 0
            ? []
            : await _db.LienOffers.AsNoTracking()
                .Where(o => o.TenantId == tenantId
                    && o.SellerOrgId == sellerOrgId
                    && lienIds.Contains(o.LienId))
                .ToListAsync(ct);

        return new AnalyticsData(liens, offers);
    }

    private static SellingAnalyticsSummary BuildSummary(IReadOnlyList<Lien> liens, IReadOnlyList<LienOffer> offers)
    {
        return new SellingAnalyticsSummary
        {
            TotalCount = liens.Count,
            PortfolioValue = liens.Sum(l => l.OriginalAmount),
            AskAmount = liens.Sum(l => l.AskAmount ?? 0m),
            HighestBidAmount = liens.Sum(l => HighestBidFor(l.Id, l.HighestBidAmount, offers)),
            SoldAmount = liens.Where(IsSoldForAnalytics).Sum(l => l.PurchasePrice ?? 0m),
            // Retained for response compatibility with the legacy analytics DTO.
            // Selling V2 never emits a seller-facing Draft status.
            DraftCount = 0,
            PendingCount = liens.Count(l => StatusFor(l) == SellingLienStatus.Pending),
            InternalCount = liens.Count(l => StatusFor(l) == SellingLienStatus.Internal),
            PreparedForSaleCount = liens.Count(l => StatusFor(l) == SellingLienStatus.PreparedForSale),
            SubmittedForSaleCount = liens.Count(l => StatusFor(l) == SellingLienStatus.SubmittedForSale),
            AcceptedCount = liens.Count(l => StatusFor(l) == SellingLienStatus.Accepted),
            DeclinedCount = liens.Count(l => StatusFor(l) == SellingLienStatus.Declined),
            SoldCount = liens.Count(IsSoldForAnalytics),
            WithdrawnCount = liens.Count(l => StatusFor(l) == SellingLienStatus.Withdrawn),
            ArchivedCount = liens.Count(l => StatusFor(l) == SellingLienStatus.Archived),
        };
    }

    private static SellingAnalyticsTimeseriesPoint BuildTimeseriesPoint(
        DateOnly bucket,
        IReadOnlyList<Lien> liens,
        IReadOnlyList<LienOffer> bucketOffers,
        IReadOnlyList<LienOffer> allOffers)
    {
        return new SellingAnalyticsTimeseriesPoint
        {
            BucketStart = bucket,
            LienCount = liens.Count,
            PortfolioValue = liens.Sum(l => l.OriginalAmount),
            AskAmount = liens.Sum(l => l.AskAmount ?? 0m),
            HighestBidAmount = liens.Sum(l => HighestBidFor(l.Id, l.HighestBidAmount, allOffers)),
            SoldAmount = liens.Where(IsSoldForAnalytics).Sum(l => l.PurchasePrice ?? 0m),
            OfferCount = bucketOffers.Count,
        };
    }

    private static IEnumerable<LienOffer> FilterOffersByDate(IEnumerable<LienOffer> offers, SellingAnalyticsFilter filter)
    {
        return offers.Where(o => DateWithin(o.OfferedAtUtc, filter));
    }

    private static decimal HighestBidFor(Guid lienId, decimal? storedHighestBid, IEnumerable<LienOffer> offers)
    {
        var offerHighest = offers
            .Where(o => o.LienId == lienId)
            .Where(IsEligibleOffer)
            .Select(o => (decimal?)o.OfferAmount)
            .Max();

        return offerHighest ?? storedHighestBid ?? 0m;
    }

    private static bool IsEligibleOffer(LienOffer offer)
    {
        return offer.Status != OfferStatus.Rejected
            && offer.Status != OfferStatus.Withdrawn
            && offer.Status != OfferStatus.Expired
            && !offer.IsExpired;
    }

    private static bool IsSoldForAnalytics(Lien lien)
    {
        return StatusFor(lien) == SellingLienStatus.Sold
            && lien.SoldAtUtc.HasValue
            && lien.PurchasePrice.HasValue;
    }

    private static string StatusFor(Lien lien)
    {
        if (!string.IsNullOrWhiteSpace(lien.SellerStatus))
            return lien.SellerStatus;

        return lien.Status switch
        {
            LienStatus.Sold => SellingLienStatus.Sold,
            LienStatus.Declined => SellingLienStatus.Declined,
            LienStatus.Withdrawn => SellingLienStatus.Withdrawn,
            LienStatus.Accepted => SellingLienStatus.Accepted,
            LienStatus.Offered or LienStatus.UnderReview => SellingLienStatus.SubmittedForSale,
            _ => SellingLienStatus.Pending,
        };
    }

    private static DateTime StatusAnchorFor(Lien lien)
    {
        return StatusFor(lien) switch
        {
            SellingLienStatus.SubmittedForSale => lien.SubmittedForSaleAtUtc ?? lien.UpdatedAtUtc,
            SellingLienStatus.Accepted => lien.UpdatedAtUtc,
            SellingLienStatus.Declined => lien.ClosedAtUtc ?? lien.UpdatedAtUtc,
            SellingLienStatus.Sold => lien.SoldAtUtc ?? lien.ClosedAtUtc ?? lien.UpdatedAtUtc,
            SellingLienStatus.Withdrawn => lien.WithdrawnAtUtc ?? lien.ClosedAtUtc ?? lien.UpdatedAtUtc,
            SellingLienStatus.Archived => lien.ArchivedAtUtc ?? lien.UpdatedAtUtc,
            _ => lien.UpdatedAtUtc,
        };
    }

    private static DateTime? DateAnchorFor(Lien lien, string dateDimension)
    {
        return dateDimension switch
        {
            "sold" => lien.SoldAtUtc,
            "service" => lien.InitialServiceDate.HasValue
                ? lien.InitialServiceDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                : null,
            _ => lien.SubmittedForSaleAtUtc,
        };
    }

    private static bool DateWithin(DateTime? value, SellingAnalyticsFilter filter)
    {
        if (!value.HasValue)
            return false;

        return DateWithin(value.Value, filter);
    }

    private static bool DateWithin(DateTime value, SellingAnalyticsFilter filter)
    {
        var date = value.Date;
        if (filter.StartDate.HasValue && date < filter.StartDate.Value.ToDateTime(TimeOnly.MinValue))
            return false;
        if (filter.EndDate.HasValue && date >= filter.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue))
            return false;
        return true;
    }

    private static DateOnly BucketStart(DateOnly date, string grain)
    {
        return grain switch
        {
            "day" => date,
            "week" => date.AddDays(-DaysSinceMonday(date)),
            _ => new DateOnly(date.Year, date.Month, 1),
        };
    }

    private static int DaysSinceMonday(DateOnly date)
    {
        return date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - (int)DayOfWeek.Monday;
    }

    private static decimal Percentage(int part, int total)
    {
        return total == 0 ? 0m : decimal.Round(part * 100m / total, 2);
    }

    private static ConcentrationKey ConcentrationValue(Lien lien, string dimension)
    {
        return dimension switch
        {
            "facility" => Key(lien.FacilityId),
            "fundingCompany" => Key(EffectiveFundingCompanyId(lien)),
            "listingVisibility" => new ConcentrationKey(
                lien.ListingVisibility ?? SellingListingVisibility.Private,
                lien.ListingVisibility ?? SellingListingVisibility.Private),
            _ => new ConcentrationKey(StatusFor(lien), StatusFor(lien)),
        };

        static ConcentrationKey Key(Guid? value)
        {
            return value.HasValue
                ? new ConcentrationKey(value.Value.ToString(), value.Value.ToString())
                : new ConcentrationKey("unassigned", "Unassigned");
        }
    }

    private static string BuildExportCsv(AnalyticsData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LienId,LienNumber,SellerStatus,LienStatus,ListingVisibility,FundingCompanyId,FacilityId,OriginalAmount,AskAmount,HighestBidAmount,PurchasePrice,SubmittedForSaleAtUtc,SoldAtUtc,WithdrawnAtUtc,ArchivedAtUtc");

        foreach (var lien in data.Liens.OrderByDescending(l => l.CreatedAtUtc))
        {
            var row = new[]
            {
                lien.Id.ToString(),
                lien.LienNumber,
                StatusFor(lien),
                lien.Status,
                lien.ListingVisibility ?? string.Empty,
                EffectiveFundingCompanyId(lien)?.ToString() ?? string.Empty,
                lien.FacilityId?.ToString() ?? string.Empty,
                lien.OriginalAmount.ToString("0.00", CultureInfo.InvariantCulture),
                (lien.AskAmount ?? 0m).ToString("0.00", CultureInfo.InvariantCulture),
                HighestBidFor(lien.Id, lien.HighestBidAmount, data.Offers).ToString("0.00", CultureInfo.InvariantCulture),
                (lien.PurchasePrice ?? 0m).ToString("0.00", CultureInfo.InvariantCulture),
                lien.SubmittedForSaleAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                lien.SoldAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                lien.WithdrawnAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                lien.ArchivedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            };
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static Guid? EffectiveFundingCompanyId(Lien lien)
        => lien.FundingCompanyCompanyId ?? lien.FundingCompanyId;

    private sealed record AnalyticsData(List<Lien> Liens, List<LienOffer> Offers);

    private sealed record AgingBucketDefinition(string Label, int MinDays, int MaxDays);

    private sealed record ConcentrationKey(string Value, string Label);
}
