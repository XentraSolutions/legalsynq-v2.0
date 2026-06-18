using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.DTOs;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class SellingPortfolioEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingPortfolioEndpointTests(LiensApiFactory factory) => _factory = factory;

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
    public async Task CreatePortfolio_returns_created_with_lien_snapshot_and_initial_history()
    {
        var (caseId, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: "ehr-case-123",
            lienExternalId: "ehr-lien-456",
            lienNumber: $"LIEN-SELL-{Guid.NewGuid():N}");

        var request = new CreateSellingPortfolioRequest
        {
            PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
            Name = "June imaging sale pool",
            Description = "Initial Las Vegas Imaging sale portfolio",
            LienIds = [lienId],
            BuyerOrgIds = [SeedHelper.FundingCompanyId],
        };

        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBe(Guid.Empty);
        body.Status.Should().Be(SellingPortfolioStatus.Draft);
        body.SellerOrgId.Should().Be(SeedHelper.OrgId);
        body.LienCount.Should().Be(1);
        body.OriginalAmountTotal.Should().Be(12345m);
        body.Liens.Should().ContainSingle();
        body.Liens[0].LienId.Should().Be(lienId);
        body.Liens[0].CaseId.Should().Be(caseId);
        body.Liens[0].CaseExternalId.Should().Be("ehr-case-123");
        body.Liens[0].LienExternalId.Should().Be("ehr-lien-456");
        body.Liens[0].LienLifecycleStatus.Should().Be(LienStatus.Draft);
        body.Buyers.Should().ContainSingle(b => b.BuyerOrgId == SeedHelper.FundingCompanyId);

        var historyResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{body.Id}/status-history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await historyResponse.Content.ReadFromJsonAsync<List<SellingPortfolioStatusHistoryResponse>>();
        history.Should().NotBeNull();
        history.Should().ContainSingle();
        history![0].FromStatus.Should().BeNull();
        history[0].ToStatus.Should().Be(SellingPortfolioStatus.Draft);
    }

    [Fact]
    public async Task TransitionStatus_allows_valid_transition_and_records_history()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest
            {
                Status = SellingPortfolioStatus.ReadyForReview,
                Notes = "Ready for operations review",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(SellingPortfolioStatus.ReadyForReview);

        var historyResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/status-history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<SellingPortfolioStatusHistoryResponse>>();
        history.Should().NotBeNull();
        history!.Should().Contain(h =>
            h.FromStatus == SellingPortfolioStatus.Draft &&
            h.ToStatus == SellingPortfolioStatus.ReadyForReview &&
            h.Notes == "Ready for operations review");
    }

    [Fact]
    public async Task TransitionStatus_blocks_invalid_transition()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest
            {
                Status = SellingPortfolioStatus.Accepted,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unchanged = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");
        unchanged.Should().NotBeNull();
        unchanged!.Status.Should().Be(SellingPortfolioStatus.Draft);
    }

    [Fact]
    public async Task DirectReads_deny_other_seller_org_portfolios()
    {
        var otherOrgId = Guid.Parse("30000000-0000-0000-0000-000000000099");
        var otherUserId = Guid.Parse("20000000-0000-0000-0000-000000000099");
        var portfolio = await CreatePortfolioAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, otherUserId, otherOrgId));

        var detailResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var historyResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/status-history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddLiensAndBuyers_persists_appended_children()
    {
        var portfolio = await CreatePortfolioAsync();
        var (_, secondLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        var addLiensResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                LienIds = [secondLienId],
            });

        addLiensResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var addBuyersResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/buyers",
            new AddSellingPortfolioBuyersRequest
            {
                BuyerOrgIds = [SeedHelper.FundingCompanyId],
            });

        addBuyersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reloaded = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");

        reloaded.Should().NotBeNull();
        reloaded!.Liens.Should().Contain(l => l.LienId == portfolio.Liens[0].LienId);
        reloaded.Liens.Should().Contain(l => l.LienId == secondLienId);
        reloaded.LienCount.Should().Be(2);
        reloaded.Buyers.Should().ContainSingle(b => b.BuyerOrgId == SeedHelper.FundingCompanyId);
    }

    private async Task<SellingPortfolioResponse> CreatePortfolioAsync()
    {
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Transition test portfolio",
                LienIds = [lienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
    }

    private async Task<(Guid CaseId, Guid LienId)> SeedExternalCaseAndLienAsync(
        string caseExternalId,
        string lienExternalId,
        string lienNumber)
    {
        var caseId = Guid.CreateVersion7();
        var lienId = Guid.CreateVersion7();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var caseEntity = Case.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            $"CASE-{Guid.NewGuid():N}"[..20],
            "External",
            "Client",
            SeedHelper.UserId,
            externalReference: caseExternalId);

        SetId(caseEntity, caseId);
        db.Cases.Add(caseEntity);

        var lien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            lienNumber,
            LienType.MedicalLien,
            12345m,
            SeedHelper.UserId,
            externalReference: lienExternalId,
            caseId: caseId);

        SetId(lien, lienId);
        db.Liens.Add(lien);

        await db.SaveChangesAsync();
        return (caseId, lienId);
    }

    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }
}
