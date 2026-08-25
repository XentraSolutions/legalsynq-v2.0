using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Services;

public sealed class SellingReceivablesDashboardService : ISellingReceivablesDashboardService
{
    private static readonly string[] TerminalStatuses = LienStatus.Terminal.ToArray();

    private static readonly (string Key, string Label)[] AgingBucketDefinitions =
    [
        ("0_30", "0-30 Days"),
        ("31_60", "31-60 Days"),
        ("61_90", "61-90 Days"),
        ("91_120", "91-120 Days"),
        ("120_plus", "120+ Days"),
    ];

    private static readonly (string Key, string Label)[] StatusDefinitions =
    [
        ("active", "Active"),
        ("settled", "Settled"),
        ("inReduction", "In Reduction"),
        ("paid", "Paid"),
        ("otherClosed", "Other / Closed"),
    ];

    private readonly LiensDbContext _db;
    private readonly TimeProvider _timeProvider;

    public SellingReceivablesDashboardService(LiensDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<SellingReceivablesDashboardResponse> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingReceivablesDashboardRequest request,
        CancellationToken ct = default)
    {
        var liens = _db.Liens.AsNoTracking()
            .Where(lien => lien.TenantId == tenantId &&
                           (lien.SellingOrgId == sellerOrgId || lien.OrgId == sellerOrgId) &&
                           lien.ArchivedAtUtc == null &&
                           lien.SellerStatus != SellingLienStatus.Archived);

        var summary = await liens
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalLiens = group.Count(),
                TotalReceivables = group.Sum(lien => lien.OriginalAmount),
                TotalOutstanding = group.Sum(lien =>
                    lien.CurrentBalance.HasValue && lien.CurrentBalance.Value > 0m
                        ? lien.CurrentBalance.Value
                        : 0m),
                PastDueBalance = group.Sum(lien =>
                    lien.ReceivableDueDate.HasValue &&
                    lien.ReceivableDueDate.Value < request.AsOfDate &&
                    lien.CurrentBalance.HasValue &&
                    lien.CurrentBalance.Value > 0m
                        ? lien.CurrentBalance.Value
                        : 0m),
            })
            .SingleOrDefaultAsync(ct);

        var totalLiens = summary?.TotalLiens ?? 0;
        var totalReceivables = summary?.TotalReceivables ?? 0m;
        var totalOutstanding = summary?.TotalOutstanding ?? 0m;
        var pastDueBalance = summary?.PastDueBalance ?? 0m;

        var monthStart = new DateOnly(request.AsOfDate.Year, request.AsOfDate.Month, 1);
        var payments = _db.SettlementPaymentDetails.AsNoTracking()
            .Join(liens, payment => payment.LienId, lien => lien.Id, (payment, _) => payment)
            .Where(payment => payment.TenantId == tenantId && !payment.IsDeleted);
        var paymentsReceived = await payments
            .Where(payment => payment.PaymentDate.HasValue &&
                              payment.PaymentDate.Value >= monthStart &&
                              payment.PaymentDate.Value <= request.AsOfDate)
            .SumAsync(payment => (decimal?)payment.Amount, ct) ?? 0m;
        var undatedPaymentCount = await payments.CountAsync(payment => !payment.PaymentDate.HasValue, ct);

        var days30 = request.AsOfDate.AddDays(-30);
        var days60 = request.AsOfDate.AddDays(-60);
        var days90 = request.AsOfDate.AddDays(-90);
        var days120 = request.AsOfDate.AddDays(-120);
        var agingAggregates = await liens
            .Where(lien => lien.CurrentBalance.HasValue && lien.CurrentBalance.Value > 0m)
            .Select(lien => new
            {
                Bucket = !lien.ReceivableDueDate.HasValue ? "unaged" :
                    lien.ReceivableDueDate.Value >= days30 ? "0_30" :
                    lien.ReceivableDueDate.Value >= days60 ? "31_60" :
                    lien.ReceivableDueDate.Value >= days90 ? "61_90" :
                    lien.ReceivableDueDate.Value >= days120 ? "91_120" : "120_plus",
                Balance = lien.CurrentBalance!.Value,
            })
            .GroupBy(row => row.Bucket)
            .Select(group => new AgingAggregate(group.Key, group.Count(), group.Sum(row => row.Balance)))
            .ToListAsync(ct);
        var agingSummary = BuildAgingSummary(agingAggregates, totalOutstanding);

        var settlementTotals = _db.LienSettlements.AsNoTracking()
            .Where(settlement => settlement.TenantId == tenantId && !settlement.IsDeleted)
            .GroupBy(settlement => settlement.LienId)
            .Select(group => new { LienId = group.Key, Amount = group.Sum(item => item.Amount) });
        var paymentTotals = _db.SettlementPaymentDetails.AsNoTracking()
            .Where(payment => payment.TenantId == tenantId && !payment.IsDeleted)
            .GroupBy(payment => payment.LienId)
            .Select(group => new { LienId = group.Key, Amount = group.Sum(item => item.Amount) });
        var reductionFlags = _db.LienReductions.AsNoTracking()
            .Where(reduction => reduction.TenantId == tenantId && !reduction.IsDeleted)
            .GroupBy(reduction => reduction.LienId)
            .Select(group => new { LienId = group.Key });

        var lienFacts =
            from lien in liens
            join settlementTotal in settlementTotals on lien.Id equals settlementTotal.LienId into settlementJoin
            from settlementTotal in settlementJoin.DefaultIfEmpty()
            join paymentTotal in paymentTotals on lien.Id equals paymentTotal.LienId into paymentJoin
            from paymentTotal in paymentJoin.DefaultIfEmpty()
            join reductionFlag in reductionFlags on lien.Id equals reductionFlag.LienId into reductionJoin
            from reductionFlag in reductionJoin.DefaultIfEmpty()
            select new
            {
                lien.Id,
                lien.FundingCompanyCompanyId,
                lien.FundingCompanyId,
                Balance = lien.CurrentBalance.HasValue && lien.CurrentBalance.Value > 0m
                    ? lien.CurrentBalance.Value
                    : 0m,
                OperationalStatus = settlementTotal != null && settlementTotal.Amount > 0m &&
                                    paymentTotal != null && paymentTotal.Amount >= settlementTotal.Amount
                    ? "paid"
                    : settlementTotal != null || lien.Status == LienStatus.Settled
                        ? "settled"
                        : reductionFlag != null
                            ? "inReduction"
                            : TerminalStatuses.Contains(lien.Status) ? "otherClosed" : "active",
            };

        var statusAggregates = await lienFacts
            .GroupBy(row => row.OperationalStatus)
            .Select(group => new StatusAggregate(group.Key, group.Count()))
            .ToListAsync(ct);
        var statusBreakdown = BuildStatusBreakdown(statusAggregates, totalLiens);

        var unassignedBuyerCount = await lienFacts.CountAsync(row =>
            row.Balance > 0m && !row.FundingCompanyCompanyId.HasValue && !row.FundingCompanyId.HasValue, ct);

        var canonicalRaw = await lienFacts
            .Where(row => row.Balance > 0m && row.FundingCompanyCompanyId.HasValue)
            .GroupBy(row => row.FundingCompanyCompanyId!.Value)
            .Select(group => new
            {
                BuyerId = group.Key,
                ActiveLienCount = group.Count(row => row.OperationalStatus == "active"),
                OutstandingBalance = group.Sum(row => row.Balance),
                LienCount = group.Count(),
            })
            .ToListAsync(ct);
        var canonicalIds = canonicalRaw.Select(group => group.BuyerId).ToList();
        var canonicalNames = await _db.Companies.AsNoTracking()
            .Where(company => company.TenantId == tenantId &&
                              company.OrgId == sellerOrgId &&
                              company.IsActive &&
                              company.CompanyTypeId == CompanyDirectoryReferenceData.FundingCompanyId &&
                              canonicalIds.Contains(company.Id))
            .ToDictionaryAsync(company => company.Id, company => company.Name, ct);
        unassignedBuyerCount += canonicalRaw
            .Where(group => !canonicalNames.ContainsKey(group.BuyerId))
            .Sum(group => group.LienCount);
        var canonicalGroups = canonicalRaw
            .Where(group => canonicalNames.ContainsKey(group.BuyerId))
            .Select(group => new BuyerAggregate(
                group.BuyerId,
                true,
                canonicalNames[group.BuyerId],
                group.ActiveLienCount,
                group.OutstandingBalance,
                [group.BuyerId]))
            .ToList();

        var legacyRaw = await lienFacts
            .Where(row => row.Balance > 0m && !row.FundingCompanyCompanyId.HasValue && row.FundingCompanyId.HasValue)
            .GroupBy(row => row.FundingCompanyId!.Value)
            .Select(group => new
            {
                ReferenceId = group.Key,
                ActiveLienCount = group.Count(row => row.OperationalStatus == "active"),
                OutstandingBalance = group.Sum(row => row.Balance),
                LienCount = group.Count(),
            })
            .ToListAsync(ct);
        var legacyReferenceIds = legacyRaw.Select(group => group.ReferenceId).ToList();
        var legacyContacts = await _db.Contacts.AsNoTracking()
            .Where(contact => contact.TenantId == tenantId &&
                              contact.IsActive &&
                              (contact.ContactType == ContactType.FundingCompany ||
                               contact.ContactType == ContactType.LienHolder) &&
                              (legacyReferenceIds.Contains(contact.Id) || legacyReferenceIds.Contains(contact.OrgId)))
            .Select(contact => new LegacyContact(
                contact.Id,
                contact.OrgId,
                contact.IsActive,
                string.IsNullOrWhiteSpace(contact.Organization) ? contact.DisplayName : contact.Organization!))
            .ToListAsync(ct);
        var contactsById = legacyContacts
            .GroupBy(contact => contact.Id)
            .ToDictionary(group => group.Key, group => PreferredContact(group));
        var contactsByOrgId = legacyContacts
            .GroupBy(contact => contact.OrgId)
            .ToDictionary(group => group.Key, group => PreferredContact(group));
        var legacyResolutionByReference = legacyReferenceIds
            .Distinct()
            .Select(referenceId => new { ReferenceId = referenceId, Contact = ResolveLegacyContact(referenceId, contactsById, contactsByOrgId) })
            .Where(item => item.Contact is not null)
            .ToDictionary(item => item.ReferenceId, item => item.Contact!);
        unassignedBuyerCount += legacyRaw
            .Where(group => !legacyResolutionByReference.ContainsKey(group.ReferenceId))
            .Sum(group => group.LienCount);
        var legacyGroups = legacyRaw
            .Where(group => legacyResolutionByReference.ContainsKey(group.ReferenceId))
            .GroupBy(group => legacyResolutionByReference[group.ReferenceId].Id)
            .Select(group =>
            {
                var contact = legacyResolutionByReference[group.First().ReferenceId];
                return new BuyerAggregate(
                    contact.Id,
                    false,
                    contact.Name,
                    group.Sum(item => item.ActiveLienCount),
                    group.Sum(item => item.OutstandingBalance),
                    group.Select(item => item.ReferenceId).Distinct().ToArray());
            })
            .ToList();

        var validBuyerGroups = canonicalGroups
            .Concat(legacyGroups)
            .OrderByDescending(group => group.OutstandingBalance)
            .ThenBy(group => group.BuyerName, StringComparer.OrdinalIgnoreCase)
            .Take(request.TopBuyerLimit)
            .ToList();
        var topBuyers = validBuyerGroups.Select(group => new ReceivablesBuyerSummary
        {
            BuyerId = group.BuyerId,
            BuyerName = group.BuyerName,
            Initials = BuildInitials(group.BuyerName),
            ActiveLienCount = group.ActiveLienCount,
            OutstandingBalance = group.OutstandingBalance,
            PercentOfOutstanding = Percent(group.OutstandingBalance, totalOutstanding),
        }).ToList();

        var selectedCanonicalIds = validBuyerGroups.Where(group => group.IsCanonical).Select(group => group.BuyerId).ToList();
        var selectedLegacyReferenceIds = validBuyerGroups
            .Where(group => !group.IsCanonical)
            .SelectMany(group => group.SourceReferenceIds)
            .Distinct()
            .ToList();
        var rawBuyerAgingRows = await liens
            .Where(lien => lien.CurrentBalance.HasValue && lien.CurrentBalance.Value > 0m &&
                           ((lien.FundingCompanyCompanyId.HasValue && selectedCanonicalIds.Contains(lien.FundingCompanyCompanyId.Value)) ||
                            (!lien.FundingCompanyCompanyId.HasValue && lien.FundingCompanyId.HasValue &&
                             selectedLegacyReferenceIds.Contains(lien.FundingCompanyId.Value))))
            .Select(lien => new
            {
                ReferenceId = lien.FundingCompanyCompanyId ?? lien.FundingCompanyId!.Value,
                IsCanonical = lien.FundingCompanyCompanyId.HasValue,
                lien.ReceivableDueDate,
                Balance = lien.CurrentBalance!.Value,
            })
            .ToListAsync(ct);
        var buyerAgingRows = rawBuyerAgingRows.Select(row => new BuyerAgingRow(
            row.IsCanonical ? row.ReferenceId : legacyResolutionByReference[row.ReferenceId].Id,
            row.IsCanonical,
            row.ReceivableDueDate,
            row.Balance)).ToList();
        var buyerAging = validBuyerGroups.Select(group => BuildBuyerAging(
            group,
            buyerAgingRows.Where(row => row.BuyerId == group.BuyerId && row.IsCanonical == group.IsCanonical),
            request.AsOfDate)).ToList();

        return new SellingReceivablesDashboardResponse
        {
            AsOfDate = request.AsOfDate,
            Summary = new ReceivablesDashboardSummary
            {
                TotalReceivables = Metric(totalReceivables),
                TotalOutstanding = Metric(totalOutstanding),
                PastDueBalance = Metric(pastDueBalance),
                PaymentsReceived = Metric(paymentsReceived),
            },
            AgingSummary = agingSummary,
            StatusBreakdown = statusBreakdown,
            LiensOverTime = BuildTimePoints(request, totalOutstanding, totalLiens),
            TopBuyers = topBuyers,
            BuyerAging = buyerAging,
            DataQuality = new ReceivablesDataQuality
            {
                MissingDueDateCount = agingSummary.UnagedLienCount,
                UnassignedBuyerCount = unassignedBuyerCount,
                UndatedPaymentCount = undatedPaymentCount,
                HistoricalSnapshotsAvailable = false,
            },
        };
    }

    private List<ReceivablesTimePoint> BuildTimePoints(
        SellingReceivablesDashboardRequest request,
        decimal totalOutstanding,
        int totalLiens)
    {
        var utcToday = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var currentPeriod = new DateOnly(request.AsOfDate.Year, request.AsOfDate.Month, 1);
        var firstPeriod = currentPeriod.AddMonths(-(request.Months - 1));
        return Enumerable.Range(0, request.Months)
            .Select(offset => firstPeriod.AddMonths(offset))
            .Select(periodStart =>
            {
                var available = request.AsOfDate == utcToday && periodStart == currentPeriod;
                return new ReceivablesTimePoint
                {
                    PeriodStart = periodStart,
                    OutstandingBalance = available ? totalOutstanding : null,
                    LienCount = available ? totalLiens : null,
                    DataAvailable = available,
                };
            })
            .ToList();
    }

    private static ReceivablesAgingSummary BuildAgingSummary(
        IReadOnlyCollection<AgingAggregate> aggregates,
        decimal totalOutstanding)
    {
        var byKey = aggregates.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var unaged = byKey.GetValueOrDefault("unaged");
        return new ReceivablesAgingSummary
        {
            TotalOutstanding = totalOutstanding,
            Buckets = AgingBucketDefinitions.Select(definition =>
            {
                var aggregate = byKey.GetValueOrDefault(definition.Key);
                var amount = aggregate?.Amount ?? 0m;
                return new ReceivablesAgingBucket
                {
                    Key = definition.Key,
                    Label = definition.Label,
                    LienCount = aggregate?.Count ?? 0,
                    Amount = amount,
                    Percent = Percent(amount, totalOutstanding),
                };
            }).ToList(),
            UnagedLienCount = unaged?.Count ?? 0,
            UnagedBalance = unaged?.Amount ?? 0m,
        };
    }

    private static ReceivablesStatusBreakdown BuildStatusBreakdown(
        IReadOnlyCollection<StatusAggregate> aggregates,
        int totalLiens)
    {
        var counts = aggregates.ToDictionary(item => item.Key, item => item.Count, StringComparer.Ordinal);
        return new ReceivablesStatusBreakdown
        {
            TotalLiens = totalLiens,
            Items = StatusDefinitions.Select(definition =>
            {
                var count = counts.GetValueOrDefault(definition.Key);
                return new ReceivablesStatusItem
                {
                    Key = definition.Key,
                    Label = definition.Label,
                    Count = count,
                    Percent = Percent(count, totalLiens),
                };
            }).ToList(),
        };
    }

    private static ReceivablesBuyerAging BuildBuyerAging(
        BuyerAggregate group,
        IEnumerable<BuyerAgingRow> rows,
        DateOnly asOfDate)
    {
        var buckets = rows
            .GroupBy(row => BucketKey(row.ReceivableDueDate, asOfDate))
            .ToDictionary(bucket => bucket.Key, bucket => bucket.Sum(row => row.Balance), StringComparer.Ordinal);
        var overThirtyDays = buckets.GetValueOrDefault("31_60") + buckets.GetValueOrDefault("61_90") +
                             buckets.GetValueOrDefault("91_120") + buckets.GetValueOrDefault("120_plus");
        var pastDuePercent = Percent(overThirtyDays, group.OutstandingBalance);
        return new ReceivablesBuyerAging
        {
            BuyerId = group.BuyerId,
            BuyerName = group.BuyerName,
            OutstandingBalance = group.OutstandingBalance,
            PastDuePercent = pastDuePercent,
            RiskLevel = ReceivablesDashboardPolicy.ResolveRiskLevel(pastDuePercent),
            Buckets = new ReceivablesBuyerAgingBuckets
            {
                Days0To30 = buckets.GetValueOrDefault("0_30"),
                Days31To60 = buckets.GetValueOrDefault("31_60"),
                Days61To90 = buckets.GetValueOrDefault("61_90"),
                Days91To120 = buckets.GetValueOrDefault("91_120"),
                Days120Plus = buckets.GetValueOrDefault("120_plus"),
                Unaged = buckets.GetValueOrDefault("unaged"),
            },
        };
    }

    private static LegacyContact PreferredContact(IEnumerable<LegacyContact> contacts) => contacts
        .OrderByDescending(contact => contact.IsActive)
        .ThenBy(contact => contact.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(contact => contact.Id)
        .First();

    private static LegacyContact? ResolveLegacyContact(
        Guid referenceId,
        IReadOnlyDictionary<Guid, LegacyContact> contactsById,
        IReadOnlyDictionary<Guid, LegacyContact> contactsByOrgId) =>
        contactsById.GetValueOrDefault(referenceId) ?? contactsByOrgId.GetValueOrDefault(referenceId);

    private static string BucketKey(DateOnly? dueDate, DateOnly asOfDate)
    {
        if (!dueDate.HasValue) return "unaged";
        var ageDays = Math.Max(0, asOfDate.DayNumber - dueDate.Value.DayNumber);
        return ageDays switch
        {
            <= 30 => "0_30",
            <= 60 => "31_60",
            <= 90 => "61_90",
            <= 120 => "91_120",
            _ => "120_plus",
        };
    }

    private static ReceivablesMetric Metric(decimal amount) => new()
    {
        Amount = amount,
        TrendPercent = null,
        TrendAvailable = false,
    };

    private static decimal Percent(decimal numerator, decimal denominator) => denominator <= 0m
        ? 0m
        : Math.Round(numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);

    private static string BuildInitials(string name) => string.Concat(name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Take(2)
        .Select(word => char.ToUpperInvariant(word[0])));

    private sealed record AgingAggregate(string Key, int Count, decimal Amount);
    private sealed record StatusAggregate(string Key, int Count);
    private sealed record LegacyContact(Guid Id, Guid OrgId, bool IsActive, string Name);
    private sealed record BuyerAggregate(
        Guid BuyerId,
        bool IsCanonical,
        string BuyerName,
        int ActiveLienCount,
        decimal OutstandingBalance,
        IReadOnlyCollection<Guid> SourceReferenceIds);
    private sealed record BuyerAgingRow(Guid BuyerId, bool IsCanonical, DateOnly? ReceivableDueDate, decimal Balance);
}
