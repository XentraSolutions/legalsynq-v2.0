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

public class SellingDashboardEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingDashboardEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task Dashboard_returns_summary_for_all_filtered_rows_and_pending_tab_items()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var otherSellerOrgId = Guid.CreateVersion7();
        var pending = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 700m);

        await SeedDashboardLiensAsync(db =>
        {
            db.Liens.Add(pending);
            db.Liens.Add(CreateDashboardLien(fundingCompanyId, SellingLienStatus.Internal, 2_000m, 1_600m, 0m));
            db.Liens.Add(CreateDashboardLien(
                fundingCompanyId,
                SellingLienStatus.Sold,
                3_000m,
                2_400m,
                0m,
                purchasePrice: 2_500m,
                soldAtUtc: DateTime.UtcNow.AddDays(-1)));
            db.Liens.Add(CreateDashboardLien(
                fundingCompanyId,
                SellingLienStatus.SubmittedForSale,
                10_000m,
                8_000m,
                7_000m));
            db.Liens.Add(CreateDashboardLien(
                fundingCompanyId,
                SellingLienStatus.Pending,
                9_000m,
                7_200m,
                0m,
                sellerOrgId: otherSellerOrgId));
        });

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=pending&fundingCompanyId={fundingCompanyId}&sortBy=askAmount&sortDirection=desc");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.Summary.TotalPortfolioValue.Should().Be(6_000m);
        body.Summary.TotalPending.Should().Be(1_000m);
        body.Summary.TotalInternal.Should().Be(2_000m);
        body.Summary.TotalSold.Should().Be(3_000m);
        body.Summary.PendingCount.Should().Be(1);
        body.Summary.InternalCount.Should().Be(1);
        body.Summary.SoldCount.Should().Be(1);
        body.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle();
        body.Items[0].LienId.Should().Be(pending.Id);
        body.Items[0].HighestBidAmount.Should().Be(700m);
        body.Items[0].Status.Should().Be(SellingLienStatus.Pending);
    }

    [Fact]
    public async Task Dashboard_sold_tab_search_includes_accepted_liens_as_sold()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var accepted = CreateAcceptedDashboardLien(fundingCompanyId);
        await SeedDashboardLiensAsync(db => db.Liens.Add(accepted));

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=sold&search={Uri.EscapeDataString(accepted.LienNumber)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.Summary.SoldCount.Should().Be(1);
        body.Summary.TotalSold.Should().Be(accepted.OriginalAmount);
        body.Items.Should().ContainSingle();
        body.Items[0].LienId.Should().Be(accepted.Id);
        body.Items[0].Status.Should().Be(SellingLienStatus.Sold);
    }

    [Fact]
    public async Task Dashboard_highest_bid_sort_uses_grouped_offer_values_before_paging()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var buyerOrgId = Guid.CreateVersion7();
        var lowerBidLien = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            2_000m,
            1_800m,
            0m);
        var higherBidLien = CreateDashboardLien(
            fundingCompanyId,
            SellingLienStatus.Pending,
            3_000m,
            2_700m,
            0m);

        await SeedDashboardLiensAsync(db =>
        {
            db.Liens.AddRange(lowerBidLien, higherBidLien);
            db.LienOffers.Add(LienOffer.Create(
                SeedHelper.TenantId,
                lowerBidLien.Id,
                buyerOrgId,
                SeedHelper.OrgId,
                1_200m,
                SeedHelper.UserId));
            db.LienOffers.Add(LienOffer.Create(
                SeedHelper.TenantId,
                higherBidLien.Id,
                buyerOrgId,
                SeedHelper.OrgId,
                1_900m,
                SeedHelper.UserId));
        });

        var response = await _client.GetAsync(
            $"/api/liens/selling/dashboard?tab=pending&fundingCompanyId={fundingCompanyId}&sortBy=highestBid&sortDirection=desc&page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<SellingDashboardResponse>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(2);
        body.Items.Should().ContainSingle();
        body.Items[0].LienId.Should().Be(higherBidLien.Id);
        body.Items[0].HighestBidAmount.Should().Be(1_900m);
    }

    [Fact]
    public async Task Dashboard_requires_sale_read_permission()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                [LiensPermissions.LienSaleCreate]));

        var response = await _client.GetAsync("/api/liens/selling/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_rejects_unknown_tabs()
    {
        var response = await _client.GetAsync("/api/liens/selling/dashboard?tab=ready-to-sell");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lien_list_returns_the_filtered_page_without_dashboard_summary()
    {
        var fundingCompanyId = Guid.CreateVersion7();
        var pending = CreateDashboardLien(fundingCompanyId, SellingLienStatus.Pending, 1_000m, 800m, 700m);
        await SeedDashboardLiensAsync(db =>
        {
            db.Liens.Add(pending);
            db.Liens.Add(CreateDashboardLien(fundingCompanyId, SellingLienStatus.Internal, 2_000m, 1_600m, 0m));
        });

        var response = await _client.GetAsync(
            $"/api/liens/selling/liens?tab=pending&fundingCompanyId={fundingCompanyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.TryGetProperty("summary", out _).Should().BeFalse();

        var body = JsonSerializer.Deserialize<SellingLienListResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle();
        body.Items[0].LienId.Should().Be(pending.Id);
    }

    private async Task SeedDashboardLiensAsync(Action<LiensDbContext> arrange)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        arrange(db);
        await db.SaveChangesAsync();
    }

    private static Lien CreateDashboardLien(
        Guid fundingCompanyId,
        string sellerStatus,
        decimal originalAmount,
        decimal askAmount,
        decimal highestBidAmount,
        Guid? sellerOrgId = null,
        decimal? purchasePrice = null,
        DateTime? soldAtUtc = null)
    {
        var orgId = sellerOrgId ?? SeedHelper.OrgId;
        var lien = Lien.Create(
            SeedHelper.TenantId,
            orgId,
            $"LIEN-DASHBOARD-{Guid.NewGuid():N}"[..32],
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
                purchasePrice: purchasePrice);
        }

        lien.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            sellerStatus: sellerStatus,
            fundingCompanyId: fundingCompanyId,
            askAmount: askAmount,
            highestBidAmount: highestBidAmount,
            soldAtUtc: soldAtUtc);

        return lien;
    }

    private static Lien CreateAcceptedDashboardLien(Guid fundingCompanyId)
    {
        var lien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"LIEN-ACCEPTED-{Guid.NewGuid():N}"[..32],
            LienType.MedicalLien,
            3_000m,
            SeedHelper.UserId,
            caseId: SeedHelper.CaseId,
            facilityId: SeedHelper.FacilityId,
            initialServiceDate: new DateOnly(2026, 1, 15));

        lien.ListForSale(2_500m, SeedHelper.UserId);
        lien.TransitionStatus(LienStatus.Accepted, SeedHelper.UserId);
        lien.UpdateSellingAnalyticsFields(
            SeedHelper.UserId,
            sellerStatus: SellingLienStatus.Accepted,
            fundingCompanyId: fundingCompanyId,
            askAmount: 2_500m,
            highestBidAmount: 2_500m);

        return lien;
    }
}
