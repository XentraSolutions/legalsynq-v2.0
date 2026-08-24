using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.DTOs;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class SellingOperationsDashboardEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingOperationsDashboardEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_requires_authentication_analytics_permission_and_sell_mode()
    {
        using var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/liens/selling/analytics/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                [LiensPermissions.LienSaleRead]));
        (await _client.GetAsync("/api/liens/selling/analytics/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                includeProductAccess: false));
        (await _client.GetAsync("/api/liens/selling/analytics/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                providerMode: "manage"));
        (await _client.GetAsync("/api/liens/selling/analytics/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_returns_zeroes_and_explicit_unavailable_aging_for_empty_period()
    {
        var response = await _client.GetAsync(
            "/api/liens/selling/analytics/dashboard?startDate=2030-01-01&endDate=2030-01-31&compare=none");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingOperationsDashboardResponse>();
        body.Should().NotBeNull();
        body!.Currency.Should().Be("USD");
        body.ComparisonPeriod.Should().BeNull();
        body.Metrics.TotalLienRevenue.Value.Should().Be(0m);
        body.Metrics.TotalLienRevenue.Formula.Should().Contain("OriginalAmount");
        body.Metrics.TotalOutstanding.Value.Should().Be(0m);
        body.Metrics.TotalOutstanding.Formula.Should().Contain("CurrentBalance");
        body.Metrics.Payments.Value.Should().Be(0m);
        body.Metrics.PastAmountDue.IsAvailable.Should().BeFalse();
        body.Metrics.PastAmountDue.Value.Should().BeNull();
        body.Metrics.PastAmountDue.UnavailableReason.Should().Contain("due date");
        body.ArAging.IsAvailable.Should().BeFalse();
        body.ArAging.Total.Should().BeNull();
        body.ArAging.Buckets.Should().BeEmpty();
        body.BuyerAging.IsAvailable.Should().BeFalse();
        body.BuyerAging.Items.Should().BeEmpty();
        body.LienStatuses.Should().BeEmpty();
        body.SellerStatuses.Should().BeEmpty();
        body.TimeSeries.Should().BeEmpty();
        body.TopBuyers.Should().BeEmpty();
    }

    [Fact]
    public async Task Dashboard_uses_inclusive_service_and_payment_periods_with_previous_period_comparison()
    {
        var currentStart = CreateLien("CURRENT-START", new DateOnly(2026, 1, 1), 1_000m, 600m);
        var currentEnd = CreateLien("CURRENT-END", new DateOnly(2026, 1, 31), 2_000m, 1_500m);
        var previous = CreateLien("PREVIOUS", new DateOnly(2025, 12, 1), 500m, 400m);
        var outside = CreateLien("OUTSIDE", new DateOnly(2025, 11, 30), 9_000m, 9_000m);

        await SeedAsync(db =>
        {
            db.Liens.AddRange(currentStart, currentEnd, previous, outside);
            db.SettlementPaymentDetails.Add(SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                SeedHelper.CaseId,
                currentStart.Id,
                2,
                250m,
                SeedHelper.UserId,
                new DateOnly(2026, 1, 31)));
            db.SettlementPaymentDetails.Add(SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                SeedHelper.CaseId,
                previous.Id,
                3,
                100m,
                SeedHelper.UserId,
                new DateOnly(2025, 12, 31)));
        });

        var response = await _client.GetAsync(
            "/api/liens/selling/analytics/dashboard?startDate=2026-01-01&endDate=2026-01-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SellingOperationsDashboardResponse>();
        body.Should().NotBeNull();
        body!.Period.StartDate.Should().Be(new DateOnly(2026, 1, 1));
        body.Period.EndDate.Should().Be(new DateOnly(2026, 1, 31));
        body.Period.DateBasis.Should().Be("initialServiceDate");
        body.ComparisonPeriod!.StartDate.Should().Be(new DateOnly(2025, 12, 1));
        body.ComparisonPeriod.EndDate.Should().Be(new DateOnly(2025, 12, 31));
        body.Metrics.TotalLienRevenue.Value.Should().Be(3_000m);
        body.Metrics.TotalLienRevenue.ComparisonValue.Should().Be(500m);
        body.Metrics.TotalLienRevenue.ChangeAmount.Should().Be(2_500m);
        body.Metrics.TotalLienRevenue.ChangePercent.Should().Be(500m);
        body.Metrics.TotalOutstanding.Value.Should().Be(2_100m);
        body.Metrics.TotalOutstanding.ComparisonValue.Should().Be(400m);
        body.Metrics.Payments.Value.Should().Be(250m);
        body.Metrics.Payments.ComparisonValue.Should().Be(100m);
        body.TimeSeries.Should().ContainSingle();
        body.TimeSeries[0].BucketStart.Should().Be(new DateOnly(2026, 1, 1));
        body.TimeSeries[0].LienRevenue.Should().Be(3_000m);
    }

    [Fact]
    public async Task Dashboard_scopes_all_financials_to_tenant_and_seller_organization()
    {
        var included = CreateLien("CANONICAL-SELLER", new DateOnly(2026, 2, 10), 1_000m, 800m);
        SetLienOwnership(included, Guid.CreateVersion7(), SeedHelper.OrgId);
        var conflictingLegacyOrg = CreateLien(
            "CONFLICTING-LEGACY-ORG",
            new DateOnly(2026, 2, 10),
            7_000m,
            7_000m);
        SetLienOwnership(conflictingLegacyOrg, SeedHelper.OrgId, Guid.CreateVersion7());
        var otherTenant = Lien.Create(
            Guid.CreateVersion7(),
            SeedHelper.OrgId,
            "OTHER-TENANT",
            LienType.MedicalLien,
            8_000m,
            SeedHelper.UserId,
            initialServiceDate: new DateOnly(2026, 2, 10));

        await SeedAsync(db => db.Liens.AddRange(included, conflictingLegacyOrg, otherTenant));

        var response = await _client.GetAsync(
            "/api/liens/selling/analytics/dashboard?startDate=2026-02-01&endDate=2026-02-28&compare=none");

        var body = await response.Content.ReadFromJsonAsync<SellingOperationsDashboardResponse>();
        body.Should().NotBeNull();
        body!.Metrics.TotalLienRevenue.Value.Should().Be(1_000m);
        body.Metrics.TotalOutstanding.Value.Should().Be(800m);
        body.LienStatuses.Sum(item => item.LienCount).Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_keeps_accepted_distinct_and_requires_sale_evidence_for_sold()
    {
        var accepted = CreateLien("ACCEPTED", new DateOnly(2026, 3, 1), 1_000m, 1_000m);
        accepted.ListForSale(900m, SeedHelper.UserId);
        accepted.TransitionStatus(LienStatus.Accepted, SeedHelper.UserId);
        accepted.UpdateSellingAnalyticsFields(SeedHelper.UserId, sellerStatus: SellingLienStatus.Accepted);

        var sold = CreateLien("SOLD", new DateOnly(2026, 3, 2), 2_000m, 2_000m);
        sold.ListForSale(1_700m, SeedHelper.UserId);
        sold.MarkSold(1_600m, Guid.CreateVersion7(), SeedHelper.UserId);

        var incomplete = CreateLien("INCOMPLETE", new DateOnly(2026, 3, 3), 3_000m, 3_000m);
        incomplete.SetFinancials(3_000m, SeedHelper.UserId, purchasePrice: 2_500m);
        incomplete.UpdateSellingAnalyticsFields(SeedHelper.UserId, sellerStatus: SellingLienStatus.Sold);

        await SeedAsync(db => db.Liens.AddRange(accepted, sold, incomplete));

        var response = await _client.GetAsync(
            "/api/liens/selling/analytics/dashboard?startDate=2026-03-01&endDate=2026-03-31&compare=none");
        var body = await response.Content.ReadFromJsonAsync<SellingOperationsDashboardResponse>();

        body.Should().NotBeNull();
        body!.SellerStatuses.Single(item => item.Status == SellingLienStatus.Accepted).LienCount.Should().Be(1);
        body.SellerStatuses.Single(item => item.Status == SellingLienStatus.Sold).LienCount.Should().Be(1);
        body.SellerStatuses.Single(item => item.Status == "SaleIncomplete").LienCount.Should().Be(1);
        body.LienStatuses.Single(item => item.Status == LienStatus.Accepted).LienCount.Should().Be(1);
        body.LienStatuses.Single(item => item.Status == LienStatus.Sold).LienCount.Should().Be(1);
        body.LienStatuses.Single(item => item.Status == LienStatus.Draft).LienCount.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_labels_top_buyer_from_scoped_canonical_company()
    {
        var buyerOrgId = Guid.CreateVersion7();
        var olderBuyerCompany = Company.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            CompanyDirectoryReferenceData.FundingCompanyId,
            "Older Buyer Capital",
            SeedHelper.UserId);
        var selectedBuyerCompany = Company.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            CompanyDirectoryReferenceData.FundingCompanyId,
            "Selected Buyer Capital",
            SeedHelper.UserId);
        var rejectedBuyerCompany = Company.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            CompanyDirectoryReferenceData.FundingCompanyId,
            "Rejected Buyer Capital",
            SeedHelper.UserId);
        var sold = CreateLien("TOP-BUYER", new DateOnly(2026, 4, 15), 4_000m, 2_500m);
        sold.ListForSale(3_500m, SeedHelper.UserId);
        sold.MarkSold(3_000m, buyerOrgId, SeedHelper.UserId);
        var olderAcceptedOffer = LienOffer.Create(
            SeedHelper.TenantId,
            sold.Id,
            buyerOrgId,
            SeedHelper.OrgId,
            3_000m,
            SeedHelper.UserId);
        olderAcceptedOffer.LinkCanonicalBuyer(olderBuyerCompany.Id);
        olderAcceptedOffer.Accept(SeedHelper.UserId);
        SetProperty(olderAcceptedOffer, nameof(LienOffer.RespondedAtUtc), new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc));
        var selectedAcceptedOffer = LienOffer.Create(
            SeedHelper.TenantId,
            sold.Id,
            buyerOrgId,
            SeedHelper.OrgId,
            3_100m,
            SeedHelper.UserId);
        selectedAcceptedOffer.LinkCanonicalBuyer(selectedBuyerCompany.Id);
        selectedAcceptedOffer.Accept(SeedHelper.UserId);
        SetProperty(selectedAcceptedOffer, nameof(LienOffer.RespondedAtUtc), new DateTime(2026, 4, 21, 8, 0, 0, DateTimeKind.Utc));
        var rejectedOffer = LienOffer.Create(
            SeedHelper.TenantId,
            sold.Id,
            buyerOrgId,
            SeedHelper.OrgId,
            3_200m,
            SeedHelper.UserId);
        rejectedOffer.LinkCanonicalBuyer(rejectedBuyerCompany.Id);
        rejectedOffer.Reject(SeedHelper.UserId);
        SetProperty(rejectedOffer, nameof(LienOffer.RespondedAtUtc), new DateTime(2026, 4, 22, 8, 0, 0, DateTimeKind.Utc));

        var settled = CreateLien("SETTLED-PURCHASE", new DateOnly(2026, 4, 16), 2_000m, 2_000m);
        settled.ListForSale(1_500m, SeedHelper.UserId);
        settled.MarkSold(1_200m, buyerOrgId, SeedHelper.UserId);
        settled.Activate(SeedHelper.UserId);
        settled.Settle(1_100m, SeedHelper.UserId);

        await SeedAsync(db => db.AddRange(
            olderBuyerCompany,
            selectedBuyerCompany,
            rejectedBuyerCompany,
            sold,
            settled,
            olderAcceptedOffer,
            selectedAcceptedOffer,
            rejectedOffer));

        var response = await _client.GetAsync(
            "/api/liens/selling/analytics/dashboard?startDate=2026-04-01&endDate=2026-04-30&compare=none");
        var body = await response.Content.ReadFromJsonAsync<SellingOperationsDashboardResponse>();

        body.Should().NotBeNull();
        body!.TopBuyers.Should().ContainSingle();
        body.TopBuyers[0].BuyerOrgId.Should().Be(buyerOrgId);
        body.TopBuyers[0].BuyerCompanyId.Should().Be(selectedBuyerCompany.Id);
        body.TopBuyers[0].BuyerName.Should().Be("Selected Buyer Capital");
        body.TopBuyers[0].ActiveLienCount.Should().Be(1);
        body.TopBuyers[0].TotalBalance.Should().Be(2_500m);
        body.TopBuyers[0].CompletedPurchaseAmount.Should().Be(4_200m);
        body.TopBuyers[0].PercentOfTotalBalance.Should().Be(100m);
    }

    [Theory]
    [InlineData("?startDate=2026-01-01")]
    [InlineData("?startDate=2026-02-01&endDate=2026-01-01")]
    [InlineData("?startDate=2025-01-01&endDate=2026-01-02")]
    [InlineData("?startDate=0001-01-01&endDate=0001-01-01&compare=previousPeriod")]
    [InlineData("?compare=yearOverYear")]
    public async Task Dashboard_rejects_invalid_period_queries(string query)
    {
        var response = await _client.GetAsync($"/api/liens/selling/analytics/dashboard{query}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dashboard_accepts_maximum_366_day_period()
    {
        var response = await _client.GetAsync(
            "/api/liens/selling/analytics/dashboard?startDate=2025-01-01&endDate=2026-01-01&compare=none");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task SeedAsync(Action<LiensDbContext> arrange)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        arrange(db);
        await db.SaveChangesAsync();
    }

    private static Lien CreateLien(
        string suffix,
        DateOnly serviceDate,
        decimal originalAmount,
        decimal currentBalance,
        Guid? sellerOrgId = null)
    {
        var orgId = sellerOrgId ?? SeedHelper.OrgId;
        var lienNumber = $"DASH-{suffix}-{Guid.NewGuid():N}";
        var lien = Lien.Create(
            SeedHelper.TenantId,
            orgId,
            lienNumber[..Math.Min(32, lienNumber.Length)],
            LienType.MedicalLien,
            originalAmount,
            SeedHelper.UserId,
            caseId: SeedHelper.CaseId,
            initialServiceDate: serviceDate);
        lien.SetFinancials(originalAmount, SeedHelper.UserId, currentBalance: currentBalance);
        return lien;
    }

    private static void SetLienOwnership(Lien lien, Guid orgId, Guid? sellingOrgId)
    {
        SetProperty(lien, nameof(Lien.OrgId), orgId);
        SetProperty(lien, nameof(Lien.SellingOrgId), sellingOrgId);
    }

    private static void SetProperty<T>(T entity, string propertyName, object? value) where T : class
    {
        var property = typeof(T).GetProperty(propertyName);
        property.Should().NotBeNull();
        property!.SetValue(entity, value);
    }
}
