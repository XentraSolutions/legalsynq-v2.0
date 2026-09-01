using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Services;

public sealed class WeeklyAgingReportService : IWeeklyAgingReportService
{
    private readonly LiensDbContext _db;

    public WeeklyAgingReportService(LiensDbContext db) => _db = db;

    public async Task<WeeklyAgingReportResult> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var reportRows = BuildReportQuery(tenantId, sellerOrgId, asOfDate);
        var asOfStartUtc = asOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var days1To7StartUtc = SubtractDays(asOfStartUtc, 6);
        var days8To14StartUtc = SubtractDays(asOfStartUtc, 13);
        var days15To21StartUtc = SubtractDays(asOfStartUtc, 20);
        var days22To28StartUtc = SubtractDays(asOfStartUtc, 27);

        var summary = await reportRows
            .GroupBy(_ => 1)
            .Select(group => new WeeklyAgingReportTotals
            {
                TotalLiens = group.Count(),
                Days1To7 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days1To7StartUtc ? row.Amount : 0m),
                Days8To14 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days8To14StartUtc &&
                    row.BuyerAcceptedAtUtc < days1To7StartUtc ? row.Amount : 0m),
                Days15To21 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days15To21StartUtc &&
                    row.BuyerAcceptedAtUtc < days8To14StartUtc ? row.Amount : 0m),
                Days22To28 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days22To28StartUtc &&
                    row.BuyerAcceptedAtUtc < days15To21StartUtc ? row.Amount : 0m),
                MoreThan28 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc < days22To28StartUtc ? row.Amount : 0m),
                TotalAmount = group.Sum(row => row.Amount),
            })
            .SingleOrDefaultAsync(ct) ?? new WeeklyAgingReportTotals();
        var rows = await LoadPageRowsAsync(
            reportRows,
            tenantId,
            sellerOrgId,
            asOfDate,
            page,
            pageSize,
            ct);

        return new WeeklyAgingReportResult
        {
            AsOfDate = asOfDate,
            Page = page,
            PageSize = pageSize,
            TotalCount = summary.TotalLiens,
            TotalPages = CalculateTotalPages(summary.TotalLiens, pageSize),
            SummaryTotals = summary,
            Items = rows.Select(BuildWeeklyRow).ToList(),
        };
    }

    public async Task<MonthlyAgingReportResult> GetMonthlyAsync(
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var reportRows = BuildReportQuery(tenantId, sellerOrgId, asOfDate);
        var asOfStartUtc = asOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var days1To30StartUtc = SubtractDays(asOfStartUtc, 29);
        var days31To60StartUtc = SubtractDays(asOfStartUtc, 59);
        var days61To90StartUtc = SubtractDays(asOfStartUtc, 89);
        var days91To120StartUtc = SubtractDays(asOfStartUtc, 119);

        var summary = await reportRows
            .GroupBy(_ => 1)
            .Select(group => new MonthlyAgingReportTotals
            {
                TotalLiens = group.Count(),
                Days1To30 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days1To30StartUtc ? row.Amount : 0m),
                Days31To60 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days31To60StartUtc &&
                    row.BuyerAcceptedAtUtc < days1To30StartUtc ? row.Amount : 0m),
                Days61To90 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days61To90StartUtc &&
                    row.BuyerAcceptedAtUtc < days31To60StartUtc ? row.Amount : 0m),
                Days91To120 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc >= days91To120StartUtc &&
                    row.BuyerAcceptedAtUtc < days61To90StartUtc ? row.Amount : 0m),
                MoreThan120 = group.Sum(row =>
                    row.BuyerAcceptedAtUtc < days91To120StartUtc ? row.Amount : 0m),
                TotalAmount = group.Sum(row => row.Amount),
            })
            .SingleOrDefaultAsync(ct) ?? new MonthlyAgingReportTotals();
        var rows = await LoadPageRowsAsync(
            reportRows,
            tenantId,
            sellerOrgId,
            asOfDate,
            page,
            pageSize,
            ct);

        return new MonthlyAgingReportResult
        {
            AsOfDate = asOfDate,
            Page = page,
            PageSize = pageSize,
            TotalCount = summary.TotalLiens,
            TotalPages = CalculateTotalPages(summary.TotalLiens, pageSize),
            SummaryTotals = summary,
            Items = rows.Select(BuildMonthlyRow).ToList(),
        };
    }

    public async Task<WeeklyAgingDetailReportResult> GetDetailAsync(
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var reportRows = BuildReportQuery(tenantId, sellerOrgId, asOfDate);
        var totalCount = await reportRows.CountAsync(ct);
        var rows = await LoadPageRowsAsync(
            reportRows,
            tenantId,
            sellerOrgId,
            asOfDate,
            page,
            pageSize,
            ct);

        return new WeeklyAgingDetailReportResult
        {
            AsOfDate = asOfDate,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = CalculateTotalPages(totalCount, pageSize),
            Items = rows.Select(row => new WeeklyAgingDetailReportRow
            {
                LienCode = row.LienCode,
                FundingCompany = row.FundingCompany,
                Amount = row.Amount,
                AgingBucket = row.AgingDays,
            }).ToList(),
        };
    }

    private IQueryable<AcceptedAgingQueryRow> BuildReportQuery(
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate)
    {
        var asOfStartUtc = asOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var asOfEndUtc = asOfDate == DateOnly.MaxValue
            ? DateTime.MaxValue
            : asOfStartUtc.AddDays(1);
        var acceptedLinks = _db.SellingBuyerAccessLinks.AsNoTracking()
            .Where(link =>
                link.TenantId == tenantId &&
                link.SellerOrgId == sellerOrgId &&
                link.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse &&
                link.ResponseStatus == SellingBuyerResponseStatus.Accepted &&
                link.RespondedAtUtc.HasValue &&
                link.RespondedAtUtc.Value < asOfEndUtc);
        var firstAcceptanceTimes = acceptedLinks
            .GroupBy(link => link.LienId)
            .Select(group => new
            {
                LienId = group.Key,
                BuyerAcceptedAtUtc = group.Min(link => link.RespondedAtUtc),
            });
        var firstAcceptedResponses =
            from link in acceptedLinks
            join first in firstAcceptanceTimes
                on new { link.LienId, link.RespondedAtUtc }
                equals new { first.LienId, RespondedAtUtc = first.BuyerAcceptedAtUtc }
            group link by new { link.LienId, first.BuyerAcceptedAtUtc } into acceptedGroup
            select new
            {
                acceptedGroup.Key.LienId,
                BuyerAcceptedAtUtc = acceptedGroup.Key.BuyerAcceptedAtUtc!.Value,
                Amount = acceptedGroup.Max(link => link.ResponseAmount ?? 0m),
            };

        return
            from accepted in firstAcceptedResponses
            join lien in _db.Liens.AsNoTracking()
                on new { TenantId = tenantId, accepted.LienId }
                equals new { lien.TenantId, LienId = lien.Id }
            select new AcceptedAgingQueryRow
            {
                LienId = accepted.LienId,
                LienCode = lien.LienNumber,
                BuyerAcceptedAtUtc = accepted.BuyerAcceptedAtUtc,
                Amount = accepted.Amount,
            };
    }

    private async Task<List<AcceptedAgingRow>> LoadPageRowsAsync(
        IQueryable<AcceptedAgingQueryRow> reportRows,
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var offset = checked((page - 1) * pageSize);
        var pageRows = await reportRows
            .OrderBy(row => row.BuyerAcceptedAtUtc)
            .ThenBy(row => row.LienCode)
            .ThenBy(row => row.LienId)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(ct);
        if (pageRows.Count == 0)
            return [];

        var asOfStartUtc = asOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var asOfEndUtc = asOfDate == DateOnly.MaxValue
            ? DateTime.MaxValue
            : asOfStartUtc.AddDays(1);
        var lienIds = pageRows.Select(row => row.LienId).ToList();
        var acceptedLinks = await _db.SellingBuyerAccessLinks.AsNoTracking()
            .Where(link =>
                link.TenantId == tenantId &&
                link.SellerOrgId == sellerOrgId &&
                lienIds.Contains(link.LienId) &&
                link.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse &&
                link.ResponseStatus == SellingBuyerResponseStatus.Accepted &&
                link.RespondedAtUtc.HasValue &&
                link.RespondedAtUtc.Value < asOfEndUtc)
            .Select(link => new
            {
                link.Id,
                link.LienId,
                link.BuyerCompanyId,
                link.BuyerContactId,
                link.ResponseAmount,
                BuyerAcceptedAtUtc = link.RespondedAtUtc!.Value,
            })
            .ToListAsync(ct);
        var selectedLinks = acceptedLinks
            .GroupBy(link => link.LienId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(link => link.BuyerAcceptedAtUtc)
                    .ThenByDescending(link => link.ResponseAmount)
                    .ThenBy(link => link.Id)
                    .First());
        var companyIds = selectedLinks.Values
            .Where(link => link.BuyerCompanyId.HasValue)
            .Select(link => link.BuyerCompanyId!.Value)
            .Distinct()
            .ToList();
        var contactIds = selectedLinks.Values
            .Where(link => !link.BuyerCompanyId.HasValue)
            .Select(link => link.BuyerContactId)
            .Distinct()
            .ToList();
        var companyNames = companyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Companies.AsNoTracking()
                .Where(company => company.TenantId == tenantId && companyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id, company => company.Name, ct);
        var contactNames = contactIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Contacts.AsNoTracking()
                .Where(contact => contact.TenantId == tenantId && contactIds.Contains(contact.Id))
                .ToDictionaryAsync(
                    contact => contact.Id,
                    contact => contact.Organization ?? contact.DisplayName,
                    ct);

        return pageRows.Select(row =>
        {
            selectedLinks.TryGetValue(row.LienId, out var link);
            var fundingCompany = link?.BuyerCompanyId is Guid companyId
                ? companyNames.GetValueOrDefault(companyId)
                : link is null ? null : contactNames.GetValueOrDefault(link.BuyerContactId);
            var acceptedDate = DateOnly.FromDateTime(row.BuyerAcceptedAtUtc);

            return new AcceptedAgingRow(
                row.LienCode,
                fundingCompany ?? "Unknown",
                asOfDate.DayNumber - acceptedDate.DayNumber + 1,
                row.Amount);
        }).ToList();
    }

    private static WeeklyAgingReportRow BuildWeeklyRow(AcceptedAgingRow row) => new()
    {
        LienCode = row.LienCode,
        FundingCompany = row.FundingCompany,
        Days1To7 = row.AgingDays <= 7 ? row.Amount : 0m,
        Days8To14 = row.AgingDays is >= 8 and <= 14 ? row.Amount : 0m,
        Days15To21 = row.AgingDays is >= 15 and <= 21 ? row.Amount : 0m,
        Days22To28 = row.AgingDays is >= 22 and <= 28 ? row.Amount : 0m,
        MoreThan28 = row.AgingDays > 28 ? row.Amount : 0m,
        TotalAmount = row.Amount,
    };

    private static MonthlyAgingReportRow BuildMonthlyRow(AcceptedAgingRow row) => new()
    {
        LienCode = row.LienCode,
        FundingCompany = row.FundingCompany,
        Days1To30 = row.AgingDays <= 30 ? row.Amount : 0m,
        Days31To60 = row.AgingDays is >= 31 and <= 60 ? row.Amount : 0m,
        Days61To90 = row.AgingDays is >= 61 and <= 90 ? row.Amount : 0m,
        Days91To120 = row.AgingDays is >= 91 and <= 120 ? row.Amount : 0m,
        MoreThan120 = row.AgingDays > 120 ? row.Amount : 0m,
        TotalAmount = row.Amount,
    };

    private static int CalculateTotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

    private static DateTime SubtractDays(DateTime value, int days) =>
        value < DateTime.MinValue.AddDays(days) ? DateTime.MinValue : value.AddDays(-days);

    private sealed class AcceptedAgingQueryRow
    {
        public Guid LienId { get; init; }
        public string LienCode { get; init; } = string.Empty;
        public DateTime BuyerAcceptedAtUtc { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed record AcceptedAgingRow(
        string LienCode,
        string FundingCompany,
        int AgingDays,
        decimal Amount);
}
