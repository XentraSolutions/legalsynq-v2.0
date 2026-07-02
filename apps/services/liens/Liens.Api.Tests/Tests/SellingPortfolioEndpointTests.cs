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
        scope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>().Clear();
        scope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>().Clear();

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
            InternalNotes = "Seller operations only",
            TargetGrouping = "Imaging",
            LienIds = [lienId],
            BuyerOrgIds = [SeedHelper.FundingCompanyId],
        };

        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBe(Guid.Empty);
        body.Status.Should().Be(SellingPortfolioStatus.Draft);
        body.InternalNotes.Should().Be("Seller operations only");
        body.TargetGrouping.Should().Be("Imaging");
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

        var activityResponse = await _client.GetAsync($"/api/liens/selling/portfolios/{body.Id}/activity");
        activityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activity = await activityResponse.Content.ReadFromJsonAsync<List<SellingPortfolioActivityResponse>>();
        activity.Should().NotBeNull();
        activity.Should().ContainSingle(a => a.Action == "LIEN_SALE_PORTFOLIO_CREATED");
    }

    [Fact]
    public async Task Analytics_returns_financial_aging_and_activity_metrics()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/analytics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioAnalyticsResponse>();
        body.Should().NotBeNull();
        body!.PortfolioId.Should().Be(portfolio.Id);
        body.Financial.TotalReceivables.Should().Be(12345m);
        body.Financial.TotalOutstandingBalance.Should().Be(12345m);
        body.Financial.AverageLienBalance.Should().Be(12345m);
        body.Operational.LienCount.Should().Be(1);
        body.Operational.ActivityCount.Should().BeGreaterThan(0);
        body.AgingBuckets.Sum(b => b.LienCount).Should().Be(1);
    }

    [Fact]
    public async Task Analytics_uses_settlement_payment_details_for_payment_totals_and_exposure()
    {
        var portfolio = await CreatePortfolioAsync();
        var lien = portfolio.Liens.Single();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.LienSettlements.Add(LienSettlement.Create(
                SeedHelper.TenantId,
                lien.CaseId!.Value,
                lien.LienId,
                paymentNumber: 1,
                amount: 10000m,
                SeedHelper.UserId,
                status: "Pending"));
            db.SettlementPaymentDetails.Add(SettlementPaymentDetail.Create(
                SeedHelper.TenantId,
                lien.CaseId.Value,
                lien.LienId,
                paymentNumber: 1,
                amount: 2500m,
                SeedHelper.UserId,
                paymentDate: new DateOnly(2026, 6, 1),
                payee: "Provider"));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/selling/portfolios/{portfolio.Id}/analytics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioAnalyticsResponse>();
        body.Should().NotBeNull();
        body!.Financial.PaymentTotal.Should().Be(2500m);
        body.Financial.SettlementExposure.Should().Be(7500m);
    }

    [Fact]
    public async Task Publish_endpoint_promotes_draft_to_published_and_records_activity()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/publish",
            new TransitionSellingPortfolioStatusRequest { Notes = "Publish pool" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(SellingPortfolioStatus.Published);
        body.PublishedAtUtc.Should().NotBeNull();

        var history = await _client.GetFromJsonAsync<List<SellingPortfolioStatusHistoryResponse>>(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status-history");
        history.Should().NotBeNull();
        history!.Should().Contain(h => h.FromStatus == SellingPortfolioStatus.Draft && h.ToStatus == SellingPortfolioStatus.ReadyForReview);
        history.Should().Contain(h => h.FromStatus == SellingPortfolioStatus.ReadyForReview && h.ToStatus == SellingPortfolioStatus.Published);

        var activity = await _client.GetFromJsonAsync<List<SellingPortfolioActivityResponse>>(
            $"/api/liens/selling/portfolios/{portfolio.Id}/activity");
        activity.Should().NotBeNull();
        activity!.Should().Contain(a => a.Action == "LIEN_SALE_PORTFOLIO_PUBLISHED");
    }

    [Fact]
    public async Task Publish_endpoint_accepts_empty_body()
    {
        var portfolio = await CreatePortfolioAsync();

        var response = await _client.PostAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/publish",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(SellingPortfolioStatus.Published);
    }


    [Fact]
    public async Task Portfolio_lien_reuses_existing_case_reference_without_creating_duplicate_case()
    {
        var (caseId, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: "canonical-case",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        int caseCountBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            caseCountBefore = db.Cases.Count(c => c.TenantId == SeedHelper.TenantId);
        }

        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Canonical case portfolio",
                LienIds = [lienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var portfolio = (await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
        portfolio.Liens.Should().ContainSingle();
        portfolio.Liens[0].CaseId.Should().Be(caseId);
        portfolio.Liens[0].CaseExternalId.Should().Be("canonical-case");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.Cases.Count(c => c.TenantId == SeedHelper.TenantId).Should().Be(caseCountBefore);
        }
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

    [Fact]
    public async Task AddLiens_returns_partial_success_for_duplicate_ineligible_and_wrong_tenant_liens()
    {
        var portfolio = await CreatePortfolioAsync();
        var existingLienId = portfolio.Liens[0].LienId;
        var (_, eligibleLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var (_, closedLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            status: "CLOSED");
        var otherTenantLienId = await SeedOtherTenantLienAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                LienIds = [existingLienId, eligibleLienId, closedLienId, otherTenantLienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AddSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RequestedCount.Should().Be(4);
        body.AddedCount.Should().Be(1);
        body.FailedCount.Should().Be(3);
        body.Results.Should().Contain(r => r.LienId == eligibleLienId && r.Success && r.Status == "added");
        body.Results.Should().Contain(r => r.LienId == existingLienId && !r.Success && r.ReasonCode == "LIEN_ALREADY_ASSIGNED" && r.Message == "Lien is already assigned to a portfolio.");
        body.Results.Should().Contain(r => r.LienId == closedLienId && !r.Success && r.ReasonCode == "LIEN_CLOSED" && r.Message == "Closed liens cannot be assigned to a portfolio.");
        body.Results.Should().Contain(r => r.LienId == otherTenantLienId && !r.Success && r.ReasonCode == "TENANT_MISMATCH" && r.Message == "Lien tenant does not match portfolio tenant.");
        body.Portfolio.Liens.Should().Contain(l => l.LienId == existingLienId);
        body.Portfolio.Liens.Should().Contain(l => l.LienId == eligibleLienId);
        body.Portfolio.Liens.Should().NotContain(l => l.LienId == closedLienId);
        body.Portfolio.Liens.Should().NotContain(l => l.LienId == otherTenantLienId);

        var reloaded = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");
        reloaded.Should().NotBeNull();
        reloaded!.Liens.Should().Contain(l => l.LienId == eligibleLienId);
        reloaded.Liens.Should().NotContain(l => l.LienId == closedLienId);
        reloaded.Liens.Should().NotContain(l => l.LienId == otherTenantLienId);

        using var verifyScope = _factory.Services.CreateScope();
        var audit = verifyScope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>();
        audit.Events.Should().Contain(e => e.Action == "LIEN_PORTFOLIO_ELIGIBILITY_VALIDATION_FAILED");
    }

    [Fact]
    public async Task AddLiens_returns_specific_messages_for_balance_and_written_off_rules()
    {
        var portfolio = await CreatePortfolioAsync();
        var (_, zeroBalanceLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            originalAmount: 0m);
        var (_, writtenOffLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            status: "WRITTEN_OFF");

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                LienIds = [zeroBalanceLienId, writtenOffLienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AddSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.AddedCount.Should().Be(0);
        body.FailedCount.Should().Be(2);
        body.Results.Should().Contain(r =>
            r.LienId == zeroBalanceLienId &&
            r.ReasonCode == "BALANCE_NOT_POSITIVE" &&
            r.Message == "Lien balance must be greater than 0.");
        body.Results.Should().Contain(r =>
            r.LienId == writtenOffLienId &&
            r.ReasonCode == "LIEN_WRITTEN_OFF" &&
            r.Message == "Written-off liens cannot be assigned to a portfolio.");
    }

    [Fact]
    public async Task AddLiens_accepts_mixed_lien_ids_and_codes_and_separates_successes_from_failures()
    {
        var portfolio = await CreateEmptyPortfolioAsync();
        var firstLienNumber = $"LIEN-{Guid.NewGuid():N}";
        var (_, firstLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: firstLienNumber);
        var (_, secondLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens",
            new AddSellingPortfolioLiensRequest
            {
                Liens = [firstLienNumber, secondLienId.ToString(), "missing-lien-code"],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AddSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RequestedCount.Should().Be(3);
        body.AddedCount.Should().Be(2);
        body.FailedCount.Should().Be(1);
        body.SuccessfulAssignments.Should().HaveCount(2);
        body.SuccessfulAssignments.Should().Contain(r =>
            r.RequestedLien == firstLienNumber &&
            r.LienId == firstLienId &&
            r.LienCode == firstLienNumber);
        body.SuccessfulAssignments.Should().Contain(r =>
            r.RequestedLien == secondLienId.ToString() &&
            r.LienId == secondLienId);
        body.FailedAssignments.Should().ContainSingle(r =>
            r.RequestedLien == "missing-lien-code" &&
            r.ReasonCode == "LIEN_NOT_FOUND");
        body.Results.Should().HaveCount(3);
        body.Portfolio.LienCount.Should().Be(2);
        body.Portfolio.OriginalAmountTotal.Should().Be(24690m);
    }

    [Fact]
    public async Task RemoveLiens_returns_partial_success_and_recalculates_totals()
    {
        var (_, firstLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var (_, secondLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var (_, thirdLienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}");
        var missingLienId = Guid.CreateVersion7();

        var createResponse = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Removal test portfolio",
                LienIds = [firstLienId, secondLienId, thirdLienId],
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var portfolio = (await createResponse.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
        portfolio.LienCount.Should().Be(3);
        portfolio.OriginalAmountTotal.Should().Be(37035m);

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens/remove",
            new RemoveSellingPortfolioLiensRequest
            {
                LienIds = [firstLienId, secondLienId, missingLienId],
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RemoveSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RequestedCount.Should().Be(3);
        body.RemovedCount.Should().Be(2);
        body.FailedCount.Should().Be(1);
        body.Results.Should().Contain(r => r.LienId == firstLienId && r.Success && r.Status == "removed");
        body.Results.Should().Contain(r => r.LienId == secondLienId && r.Success && r.Status == "removed");
        body.Results.Should().Contain(r => r.LienId == missingLienId && !r.Success && r.ReasonCode == "LIEN_NOT_IN_PORTFOLIO");
        body.Portfolio.LienCount.Should().Be(1);
        body.Portfolio.OriginalAmountTotal.Should().Be(12345m);
        body.Portfolio.CurrentBalanceTotal.Should().Be(12345m);
        body.Portfolio.OfferPriceTotal.Should().Be(0m);
        body.Portfolio.Liens.Should().ContainSingle(l => l.LienId == thirdLienId);

        var reloaded = await _client.GetFromJsonAsync<SellingPortfolioResponse>(
            $"/api/liens/selling/portfolios/{portfolio.Id}");
        reloaded.Should().NotBeNull();
        reloaded!.LienCount.Should().Be(1);
        reloaded.OriginalAmountTotal.Should().Be(12345m);
        reloaded.Liens.Should().ContainSingle(l => l.LienId == thirdLienId);

        using var verifyScope = _factory.Services.CreateScope();
        var audit = verifyScope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>();
        var removalEvents = audit.Events.Where(e => e.Action == "LIEN_REMOVED_FROM_PORTFOLIO").ToList();
        removalEvents.Should().HaveCount(2);
        removalEvents.Should().OnlyContain(e =>
            e.EventType == "liens.selling_portfolio.lien_removed" &&
            e.TenantId == SeedHelper.TenantId &&
            e.ActorUserId == SeedHelper.UserId &&
            e.OccurredAtUtc != default);
    }

    [Fact]
    public async Task RemoveLiens_rejects_published_portfolio()
    {
        var portfolio = await CreatePortfolioAsync();
        var lienId = portfolio.Liens[0].LienId;

        var readyResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest { Status = SellingPortfolioStatus.ReadyForReview });
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/status",
            new TransitionSellingPortfolioStatusRequest { Status = SellingPortfolioStatus.Published });
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens/remove",
            new RemoveSellingPortfolioLiensRequest { LienIds = [lienId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RemoveSellingPortfolioLiensResponse>();
        body.Should().NotBeNull();
        body!.RemovedCount.Should().Be(0);
        body.FailedCount.Should().Be(1);
        body.Results.Should().ContainSingle(r =>
            r.LienId == lienId &&
            !r.Success &&
            r.ReasonCode == "PORTFOLIO_NOT_EDITABLE");
        body.Portfolio.Status.Should().Be(SellingPortfolioStatus.Published);
        body.Portfolio.Liens.Should().ContainSingle(l => l.LienId == lienId);
    }

    [Fact]
    public async Task SendBuyerEmail_sends_required_subject_and_body_to_database_contact()
    {
        var buyerContactId = Guid.CreateVersion7();
        var (_, lienId) = await SeedExternalCaseAndLienAsync(
            caseExternalId: $"case-{Guid.NewGuid():N}",
            lienExternalId: $"lien-{Guid.NewGuid():N}",
            lienNumber: $"LIEN-{Guid.NewGuid():N}",
            dateOfIncident: new DateOnly(2026, 3, 12));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var buyerContact = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.FundingCompanyId,
                ContactType.LienHolder,
                "Bailey",
                "Buyer",
                SeedHelper.UserId,
                email: "bailey.buyer@example.com");
            SetId(buyerContact, buyerContactId);
            db.Contacts.Add(buyerContact);
            await db.SaveChangesAsync();
        }

        var createResponse = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Buyer email test portfolio",
                LienIds = [lienId],
                BuyerOrgIds = [SeedHelper.FundingCompanyId],
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var portfolio = (await createResponse.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
        var lienCode = portfolio.Liens[0].LienNumber;

        var detailsUrl = $"https://app.legalsynq.test/lien/selling/{portfolio.Id}/liens/{lienId}";
        var response = await _client.PostAsJsonAsync(
            $"/api/liens/selling/portfolios/{portfolio.Id}/liens/{lienCode}/buyer-email",
            new SendLienBuyerEmailRequest
            {
                BuyerContactId = buyerContactId,
                DetailsUrl = detailsUrl,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SendLienBuyerEmailResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.BuyerName.Should().Be("Bailey Buyer");
        body.BuyerEmail.Should().Be("bailey.buyer@example.com");
        body.Subject.Should().Be($"External Client - 2026-03-12 - {lienCode}");
        body.Body.Should().Be(
            $"Hi Bailey Buyer, please find the lien details at the link below:{Environment.NewLine}{Environment.NewLine}" +
            $"{detailsUrl}{Environment.NewLine}{Environment.NewLine}" +
            "Let me know if you have any questions. Thank you.");

        using var verifyScope = _factory.Services.CreateScope();
        var publisher = verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>();
        publisher.Emails.Should().ContainSingle();
        var email = publisher.Emails[0];
        email.RecipientEmail.Should().Be("bailey.buyer@example.com");
        email.Subject.Should().Be(body.Subject);
        email.Body.Should().Be(body.Body);
        email.Metadata["lienId"].Should().Be(lienId.ToString());
        email.Metadata["buyerContactId"].Should().Be(buyerContactId.ToString());
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

    private async Task<SellingPortfolioResponse> CreateEmptyPortfolioAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/liens/selling/portfolios",
            new CreateSellingPortfolioRequest
            {
                PortfolioNumber = $"PORT-{Guid.NewGuid():N}"[..20],
                Name = "Empty test portfolio",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SellingPortfolioResponse>())!;
    }

    private async Task<(Guid CaseId, Guid LienId)> SeedExternalCaseAndLienAsync(
        string caseExternalId,
        string lienExternalId,
        string lienNumber,
        DateOnly? dateOfIncident = null,
        string? status = null,
        decimal originalAmount = 12345m)
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
            externalReference: caseExternalId,
            dateOfIncident: dateOfIncident);

        SetId(caseEntity, caseId);
        db.Cases.Add(caseEntity);

        var lien = Lien.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            lienNumber,
            LienType.MedicalLien,
            originalAmount,
            SeedHelper.UserId,
            externalReference: lienExternalId,
            caseId: caseId);

        if (status == LienStatus.Sold)
        {
            lien.ListForSale(100m, SeedHelper.UserId);
            lien.MarkSold(90m, SeedHelper.FundingCompanyId, SeedHelper.UserId);
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            SetStringProperty(lien, "Status", status);
        }

        SetId(lien, lienId);
        db.Liens.Add(lien);

        await db.SaveChangesAsync();
        return (caseId, lienId);
    }

    private async Task<Guid> SeedOtherTenantLienAsync()
    {
        var otherTenantId = Guid.Parse("10000000-0000-0000-0000-000000000099");
        var otherOrgId = Guid.Parse("30000000-0000-0000-0000-000000000099");
        var caseId = Guid.CreateVersion7();
        var lienId = Guid.CreateVersion7();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var caseEntity = Case.Create(
            otherTenantId,
            otherOrgId,
            $"CASE-{Guid.NewGuid():N}"[..20],
            "Other",
            "Tenant",
            SeedHelper.UserId);
        SetId(caseEntity, caseId);
        db.Cases.Add(caseEntity);

        var lien = Lien.Create(
            otherTenantId,
            otherOrgId,
            $"LIEN-{Guid.NewGuid():N}",
            LienType.MedicalLien,
            4000m,
            SeedHelper.UserId,
            caseId: caseId);
        SetId(lien, lienId);
        db.Liens.Add(lien);

        await db.SaveChangesAsync();
        return lienId;
    }

    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }

    private static void SetStringProperty<T>(T entity, string propertyName, string value) where T : class
    {
        var prop = typeof(T).GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, value);
    }
}
