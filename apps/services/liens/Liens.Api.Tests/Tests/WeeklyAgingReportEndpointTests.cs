using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public sealed class WeeklyAgingReportEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private static readonly DateOnly AsOfDate = new(2026, 8, 25);
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public WeeklyAgingReportEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        await RemoveTestRowsAsync(scope.ServiceProvider.GetRequiredService<LiensDbContext>());

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WeeklyAging_returns_requested_buckets_and_full_totals()
    {
        await AddAcceptedLiensAsync(
            ("DAY-01", 1, 10m),
            ("DAY-07", 7, 70m),
            ("DAY-08", 8, 80m),
            ("DAY-14", 14, 140m),
            ("DAY-15", 15, 150m),
            ("DAY-21", 21, 210m),
            ("DAY-22", 22, 220m),
            ("DAY-28", 28, 280m),
            ("DAY-29", 29, 290m));
        await AddExcludedRowsAsync();

        var response = await _client.GetAsync(
            "/api/liens/reports/weekly-aging?asOfDate=2026-08-25&page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        root.GetProperty("asOfDate").GetString().Should().Be("2026-08-25");
        root.GetProperty("currency").GetString().Should().Be("USD");
        root.GetProperty("totalCount").GetInt32().Should().Be(9);

        var totals = root.GetProperty("summaryTotals");
        totals.GetProperty("totalLiens").GetInt32().Should().Be(9);
        totals.GetProperty("days1To7").GetDecimal().Should().Be(80m);
        totals.GetProperty("days8To14").GetDecimal().Should().Be(220m);
        totals.GetProperty("days15To21").GetDecimal().Should().Be(360m);
        totals.GetProperty("days22To28").GetDecimal().Should().Be(500m);
        totals.GetProperty("moreThan28").GetDecimal().Should().Be(290m);
        totals.GetProperty("totalAmount").GetDecimal().Should().Be(1_450m);

        var rows = root.GetProperty("data").EnumerateArray()
            .ToDictionary(row => row.GetProperty("lienCode").GetString()!, row => row);
        string[] buckets = ["days1To7", "days8To14", "days15To21", "days22To28", "moreThan28"];
        rows["AGING-DAY-01"].EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(["lienCode", "fundingCompany", .. buckets, "totalAmount"]);
        AssertBucket(rows["AGING-DAY-01"], "days1To7", 10m, buckets);
        AssertBucket(rows["AGING-DAY-07"], "days1To7", 70m, buckets);
        AssertBucket(rows["AGING-DAY-08"], "days8To14", 80m, buckets);
        AssertBucket(rows["AGING-DAY-14"], "days8To14", 140m, buckets);
        AssertBucket(rows["AGING-DAY-15"], "days15To21", 150m, buckets);
        AssertBucket(rows["AGING-DAY-21"], "days15To21", 210m, buckets);
        AssertBucket(rows["AGING-DAY-22"], "days22To28", 220m, buckets);
        AssertBucket(rows["AGING-DAY-28"], "days22To28", 280m, buckets);
        AssertBucket(rows["AGING-DAY-29"], "moreThan28", 290m, buckets);
    }

    [Fact]
    public async Task MonthlyAging_returns_requested_buckets_and_full_totals()
    {
        await AddAcceptedLiensAsync(
            ("MONTH-01", 1, 10m),
            ("MONTH-30", 30, 300m),
            ("MONTH-31", 31, 310m),
            ("MONTH-60", 60, 600m),
            ("MONTH-61", 61, 610m),
            ("MONTH-90", 90, 900m),
            ("MONTH-91", 91, 910m),
            ("MONTH-120", 120, 1_200m),
            ("MONTH-121", 121, 1_210m));

        var response = await _client.GetAsync(
            "/api/liens/reports/monthly-aging?asOfDate=2026-08-25&page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        var totals = root.GetProperty("summaryTotals");
        totals.GetProperty("totalLiens").GetInt32().Should().Be(9);
        totals.GetProperty("days1To30").GetDecimal().Should().Be(310m);
        totals.GetProperty("days31To60").GetDecimal().Should().Be(910m);
        totals.GetProperty("days61To90").GetDecimal().Should().Be(1_510m);
        totals.GetProperty("days91To120").GetDecimal().Should().Be(2_110m);
        totals.GetProperty("moreThan120").GetDecimal().Should().Be(1_210m);
        totals.GetProperty("totalAmount").GetDecimal().Should().Be(6_050m);

        var rows = root.GetProperty("data").EnumerateArray()
            .ToDictionary(row => row.GetProperty("lienCode").GetString()!, row => row);
        string[] buckets = ["days1To30", "days31To60", "days61To90", "days91To120", "moreThan120"];
        rows["AGING-MONTH-01"].EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(["lienCode", "fundingCompany", .. buckets, "totalAmount"]);
        AssertBucket(rows["AGING-MONTH-01"], "days1To30", 10m, buckets);
        AssertBucket(rows["AGING-MONTH-30"], "days1To30", 300m, buckets);
        AssertBucket(rows["AGING-MONTH-31"], "days31To60", 310m, buckets);
        AssertBucket(rows["AGING-MONTH-60"], "days31To60", 600m, buckets);
        AssertBucket(rows["AGING-MONTH-61"], "days61To90", 610m, buckets);
        AssertBucket(rows["AGING-MONTH-90"], "days61To90", 900m, buckets);
        AssertBucket(rows["AGING-MONTH-91"], "days91To120", 910m, buckets);
        AssertBucket(rows["AGING-MONTH-120"], "days91To120", 1_200m, buckets);
        AssertBucket(rows["AGING-MONTH-121"], "moreThan120", 1_210m, buckets);
    }

    [Fact]
    public async Task WeeklyAgingDetail_returns_only_requested_columns_and_resolves_funding_company()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var canonicalCompany = Company.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                CompanyDirectoryReferenceData.FundingCompanyId,
                "Atlas Funding",
                SeedHelper.UserId);
            db.Companies.Add(canonicalCompany);
            AddBuyerResponse(
                db,
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "DETAIL-CANONICAL",
                AsOfDate.AddDays(-3),
                SellingBuyerResponseStatus.Accepted,
                40m,
                SeedHelper.FundingCompanyId,
                canonicalCompany.Id);
            AddBuyerResponse(
                db,
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "DETAIL-LEGACY",
                AsOfDate.AddDays(-120),
                SellingBuyerResponseStatus.Accepted,
                1_210m,
                SeedHelper.FundingCompanyId);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            "/api/liens/reports/weekly-aging-detail?asOfDate=2026-08-25&page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = payload.RootElement.GetProperty("data").EnumerateArray()
            .ToDictionary(row => row.GetProperty("lienCode").GetString()!, row => row);

        rows.Should().HaveCount(2);
        rows["AGING-DETAIL-CANONICAL"].EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("lienCode", "fundingCompany", "amount", "agingBucket");
        rows["AGING-DETAIL-CANONICAL"].GetProperty("fundingCompany").GetString()
            .Should().Be("Atlas Funding");
        rows["AGING-DETAIL-CANONICAL"].GetProperty("amount").GetDecimal().Should().Be(40m);
        rows["AGING-DETAIL-CANONICAL"].GetProperty("agingBucket").GetInt32().Should().Be(4);
        rows["AGING-DETAIL-LEGACY"].GetProperty("fundingCompany").GetString()
            .Should().Be("Capital Fund LLC");
        rows["AGING-DETAIL-LEGACY"].GetProperty("amount").GetDecimal().Should().Be(1_210m);
        rows["AGING-DETAIL-LEGACY"].GetProperty("agingBucket").GetInt32().Should().Be(121);
    }

    [Fact]
    public async Task WeeklyAging_keeps_totals_independent_of_page()
    {
        await AddAcceptedLiensAsync(
            ("PAGE-01", 1, 100m),
            ("PAGE-31", 31, 200m),
            ("PAGE-121", 121, 300m));

        var response = await _client.GetAsync(
            "/api/liens/reports/weekly-aging?asOfDate=2026-08-25&page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        root.GetProperty("page").GetInt32().Should().Be(2);
        root.GetProperty("totalCount").GetInt32().Should().Be(3);
        root.GetProperty("totalPages").GetInt32().Should().Be(2);
        root.GetProperty("data").GetArrayLength().Should().Be(1);
        root.GetProperty("summaryTotals").GetProperty("totalAmount").GetDecimal().Should().Be(600m);
    }

    [Fact]
    public async Task WeeklyAging_requires_analytics_permission_and_valid_query_values()
    {
        using var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/liens/reports/weekly-aging"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/liens/reports/monthly-aging"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                [LiensPermissions.LienSaleRead]));
        (await _client.GetAsync("/api/liens/reports/weekly-aging"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync("/api/liens/reports/monthly-aging"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                providerMode: "manage"));
        (await _client.GetAsync("/api/liens/reports/weekly-aging"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync("/api/liens/reports/monthly-aging"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
        (await _client.GetAsync("/api/liens/reports/weekly-aging?asOfDate=08%2F25%2F2026"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/liens/reports/monthly-aging?asOfDate=08%2F25%2F2026"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/liens/reports/weekly-aging?page=0&pageSize=101"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/liens/reports/weekly-aging?page=2147483647&pageSize=100"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task AddAcceptedLiensAsync(params (string Suffix, int AgingDays, decimal Amount)[] rows)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        foreach (var row in rows)
        {
            AddBuyerResponse(
                db,
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                row.Suffix,
                AsOfDate.AddDays(-(row.AgingDays - 1)),
                SellingBuyerResponseStatus.Accepted,
                row.Amount,
                SeedHelper.FundingCompanyId);
        }

        await db.SaveChangesAsync();
    }

    private async Task AddExcludedRowsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        AddBuyerResponse(
            db, SeedHelper.TenantId, SeedHelper.OrgId, "FUTURE", AsOfDate.AddDays(1),
            SellingBuyerResponseStatus.Accepted, 900m);
        AddBuyerResponse(
            db, SeedHelper.TenantId, SeedHelper.OrgId, "DECLINED", AsOfDate.AddDays(-2),
            SellingBuyerResponseStatus.Declined, null);
        AddBuyerResponse(
            db, SeedHelper.TenantId, Guid.CreateVersion7(), "OTHER-SELLER", AsOfDate.AddDays(-2),
            SellingBuyerResponseStatus.Accepted, 900m);
        AddBuyerResponse(
            db, Guid.CreateVersion7(), SeedHelper.OrgId, "OTHER-TENANT", AsOfDate.AddDays(-2),
            SellingBuyerResponseStatus.Accepted, 900m);
        await db.SaveChangesAsync();
    }

    private static void AddBuyerResponse(
        LiensDbContext db,
        Guid tenantId,
        Guid sellerOrgId,
        string suffix,
        DateOnly responseDate,
        string responseStatus,
        decimal? amount,
        Guid? buyerContactId = null,
        Guid? buyerCompanyId = null)
    {
        var lien = Lien.Create(
            tenantId,
            sellerOrgId,
            $"AGING-{suffix}",
            LienType.MedicalLien,
            amount ?? 100m,
            SeedHelper.UserId);
        var link = SellingBuyerAccessLink.Create(
            tenantId,
            lien.Id,
            sellerOrgId,
            Guid.CreateVersion7(),
            buyerContactId ?? Guid.CreateVersion7(),
            $"aging-token-{Guid.CreateVersion7():N}",
            SellingAccessLinkPurposes.ConfirmSaleBuyerResponse,
            "/selling/public",
            $"aging-key-{Guid.CreateVersion7():N}",
            DateTime.UtcNow.AddYears(1),
            SeedHelper.UserId);
        if (buyerCompanyId.HasValue)
            link.LinkCanonicalBuyer(buyerCompanyId, null);
        link.RecordResponse(responseStatus, amount, null);
        SetProperty(
            link,
            nameof(SellingBuyerAccessLink.RespondedAtUtc),
            responseDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
        db.Liens.Add(lien);
        db.SellingBuyerAccessLinks.Add(link);
    }

    private static void AssertBucket(
        JsonElement row,
        string populatedBucket,
        decimal amount,
        IReadOnlyCollection<string> buckets)
    {
        row.GetProperty("fundingCompany").GetString().Should().Be("Capital Fund LLC");
        row.GetProperty(populatedBucket).GetDecimal().Should().Be(amount);
        row.GetProperty("totalAmount").GetDecimal().Should().Be(amount);
        buckets
            .Where(bucket => bucket != populatedBucket)
            .Should().OnlyContain(bucket => row.GetProperty(bucket).GetDecimal() == 0m);
    }

    private static async Task RemoveTestRowsAsync(LiensDbContext db)
    {
        var links = await db.SellingBuyerAccessLinks
            .Where(link => db.Liens.Any(lien => lien.Id == link.LienId && lien.LienNumber.StartsWith("AGING-")))
            .ToListAsync();
        var liens = await db.Liens.Where(lien => lien.LienNumber.StartsWith("AGING-")).ToListAsync();
        db.SellingBuyerAccessLinks.RemoveRange(links);
        db.Liens.RemoveRange(liens);
        await db.SaveChangesAsync();
    }

    private static void SetProperty<T>(T entity, string propertyName, object? value) where T : class
    {
        typeof(T).GetProperty(propertyName)!.SetValue(entity, value);
    }
}
