using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.DTOs;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class SellingAnalyticsEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingAnalyticsEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Overview_counts_confirmed_submission_as_submitted_not_sold()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        await SeedAnalyticsLiensAsync(db =>
        {
            db.Liens.Add(CreateAnalyticsLien(
                fundingCompanyId,
                SellingLienStatus.SubmittedForSale,
                originalAmount: 1_000m,
                askAmount: 800m,
                submittedForSaleAtUtc: DateTime.UtcNow.AddDays(-3)));

            db.Liens.Add(CreateAnalyticsLien(
                fundingCompanyId,
                SellingLienStatus.SubmittedForSale,
                originalAmount: 2_000m,
                askAmount: 1_500m,
                submittedForSaleAtUtc: DateTime.UtcNow.AddDays(-2)));

            db.Liens.Add(CreateAnalyticsLien(
                fundingCompanyId,
                SellingLienStatus.Sold,
                originalAmount: 3_000m,
                askAmount: 2_500m,
                purchasePrice: 2_000m,
                soldAtUtc: null));

            db.Liens.Add(CreateAnalyticsLien(
                fundingCompanyId,
                SellingLienStatus.Sold,
                originalAmount: 4_000m,
                askAmount: 3_500m,
                purchasePrice: 3_000m,
                soldAtUtc: DateTime.UtcNow.AddDays(-1)));
        });

        var response = await _client.GetAsync($"/api/liens/selling/analytics/overview?fundingCompanyId={fundingCompanyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SellingAnalyticsOverviewResponse>();
        body.Should().NotBeNull();
        body!.Summary.TotalCount.Should().Be(4);
        body.Summary.SubmittedForSaleCount.Should().Be(2);
        body.Summary.SoldCount.Should().Be(1);
        body.Summary.SoldAmount.Should().Be(3_000m);
    }

    [Fact]
    public async Task Overview_denies_user_without_view_analytics_permission()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateToken(
                    SeedHelper.TenantId,
                    SeedHelper.UserId,
                    [LiensPermissions.LienSaleRead]));

        var response = await _client.GetAsync("/api/liens/selling/analytics/overview");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Buyer_only_token_is_denied_from_analytics()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateToken(
                    SeedHelper.TenantId,
                    SeedHelper.UserId,
                    [LiensPermissions.LienBrowse, LiensPermissions.LienPurchase]));

        var response = await _client.GetAsync("/api/liens/selling/analytics/overview");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Overview_scopes_results_to_current_seller_org()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var otherSellerOrgId = Guid.CreateVersion7();
        await SeedAnalyticsLiensAsync(db =>
        {
            db.Liens.Add(CreateAnalyticsLien(
                fundingCompanyId,
                SellingLienStatus.SubmittedForSale,
                sellerOrgId: SeedHelper.OrgId,
                originalAmount: 1_000m,
                askAmount: 900m));

            db.Liens.Add(CreateAnalyticsLien(
                fundingCompanyId,
                SellingLienStatus.SubmittedForSale,
                sellerOrgId: otherSellerOrgId,
                originalAmount: 9_000m,
                askAmount: 8_000m));
        });

        var response = await _client.GetAsync($"/api/liens/selling/analytics/overview?fundingCompanyId={fundingCompanyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SellingAnalyticsOverviewResponse>();
        body.Should().NotBeNull();
        body!.Summary.TotalCount.Should().Be(1);
        body.Summary.PortfolioValue.Should().Be(1_000m);
    }

    [Fact]
    public async Task Timeseries_requires_date_dimension_and_rejects_invalid_filters()
    {
        var missingDimension = await _client.GetAsync("/api/liens/selling/analytics/timeseries");
        missingDimension.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var invalidFilters = await _client.GetAsync(
            "/api/liens/selling/analytics/overview?sellerStatus=AlmostSold&dateFrom=2026-02-01&dateTo=2026-01-01");
        invalidFilters.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var errorJson = await JsonDocument.ParseAsync(await invalidFilters.Content.ReadAsStreamAsync());
        errorJson.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("validation_error");
    }

    [Fact]
    public async Task Overview_highest_bid_excludes_rejected_withdrawn_and_expired_offers()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var lien = CreateAnalyticsLien(
            fundingCompanyId,
            SellingLienStatus.SubmittedForSale,
            originalAmount: 1_000m,
            askAmount: 900m);

        await SeedAnalyticsLiensAsync(db =>
        {
            db.Liens.Add(lien);

            db.LienOffers.Add(LienOffer.Create(
                SeedHelper.TenantId, lien.Id, SeedHelper.FundingCompanyId, SeedHelper.OrgId,
                400m, SeedHelper.UserId));

            var rejected = LienOffer.Create(
                SeedHelper.TenantId, lien.Id, Guid.CreateVersion7(), SeedHelper.OrgId,
                900m, SeedHelper.UserId);
            rejected.Reject(SeedHelper.UserId);
            db.LienOffers.Add(rejected);

            var withdrawn = LienOffer.Create(
                SeedHelper.TenantId, lien.Id, Guid.CreateVersion7(), SeedHelper.OrgId,
                800m, SeedHelper.UserId);
            withdrawn.Withdraw(SeedHelper.UserId);
            db.LienOffers.Add(withdrawn);

            var expired = LienOffer.Create(
                SeedHelper.TenantId, lien.Id, Guid.CreateVersion7(), SeedHelper.OrgId,
                700m, SeedHelper.UserId);
            expired.Expire(SeedHelper.UserId);
            db.LienOffers.Add(expired);
        });

        var response = await _client.GetAsync($"/api/liens/selling/analytics/overview?fundingCompanyId={fundingCompanyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SellingAnalyticsOverviewResponse>();
        body.Should().NotBeNull();
        body!.Summary.HighestBidAmount.Should().Be(400m);
    }

    [Fact]
    public async Task Export_returns_csv_for_filtered_selling_rows()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var lien = CreateAnalyticsLien(
            fundingCompanyId,
            SellingLienStatus.SubmittedForSale,
            originalAmount: 1_000m,
            askAmount: 900m);
        await SeedAnalyticsLiensAsync(db => db.Liens.Add(lien));

        var request = new SellingAnalyticsExportRequest
        {
            Report = "overview",
            FundingCompanyId = [fundingCompanyId],
        };

        var response = await _client.PostAsJsonAsync("/api/liens/selling/analytics/export", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition?.FileNameStar.Should()
            .StartWith("selling-analytics-overview-");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("LienId,LienNumber,SellerStatus");
        csv.Should().Contain(lien.LienNumber);
    }

    [Fact]
    public async Task Selling_fields_persist_on_lien()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var contactId = Guid.CreateVersion7();
        var lien = CreateAnalyticsLien(
            fundingCompanyId,
            SellingLienStatus.PreparedForSale,
            originalAmount: 1_000m,
            askAmount: 900m,
            highestBidAmount: 850m);
        lien.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            fundingCompanyContactId: contactId,
            archivedReason: "duplicate import");

        await SeedAnalyticsLiensAsync(db => db.Liens.Add(lien));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persisted = await db.Liens.FindAsync(lien.Id);

        persisted.Should().NotBeNull();
        persisted!.SellerStatus.Should().Be(SellingLienStatus.PreparedForSale);
        persisted.ListingVisibility.Should().Be(SellingListingVisibility.Private);
        persisted.FundingCompanyId.Should().Be(fundingCompanyId);
        persisted.FundingCompanyContactId.Should().Be(contactId);
        persisted.AskAmount.Should().Be(900m);
        persisted.HighestBidAmount.Should().Be(850m);
        persisted.ArchivedReason.Should().Be("duplicate import");
    }

    private async Task SeedAnalyticsLiensAsync(Action<LiensDbContext> arrange)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        arrange(db);
        await db.SaveChangesAsync();
    }

    private static Lien CreateAnalyticsLien(
        Guid fundingCompanyId,
        string sellerStatus,
        decimal originalAmount,
        decimal askAmount,
        Guid? sellerOrgId = null,
        decimal? highestBidAmount = null,
        decimal? purchasePrice = null,
        DateTime? submittedForSaleAtUtc = null,
        DateTime? soldAtUtc = null)
    {
        var orgId = sellerOrgId ?? SeedHelper.OrgId;
        var lien = Lien.Create(
            SeedHelper.TenantId,
            orgId,
            $"LIEN-ANALYTICS-{Guid.NewGuid():N}"[..32],
            LienType.MedicalLien,
            originalAmount,
            SeedHelper.UserId,
            caseId: SeedHelper.CaseId,
            facilityId: SeedHelper.FacilityId,
            initialServiceDate: new DateOnly(2026, 1, 15));

        if (purchasePrice.HasValue)
        {
            lien.SetFinancials(
                originalAmount,
                SeedHelper.UserId,
                currentBalance: originalAmount,
                offerPrice: askAmount,
                purchasePrice: purchasePrice.Value);
        }

        lien.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            sellerStatus: sellerStatus,
            listingVisibility: SellingListingVisibility.Private,
            fundingCompanyId: fundingCompanyId,
            askAmount: askAmount,
            highestBidAmount: highestBidAmount,
            submittedForSaleAtUtc: submittedForSaleAtUtc ?? DateTime.UtcNow.AddDays(-5),
            soldAtUtc: soldAtUtc);

        return lien;
    }
}
