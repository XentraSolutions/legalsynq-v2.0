using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xenia.Application.Assistant;
using Xenia.Infrastructure.Assistant;
using Xunit;

namespace Xenia.Tests.Assistant;

public sealed class StaticAssistantToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_CareConnectLookup_ReturnsGroundedReferralOutputAndCitation()
    {
        var referralId = Guid.CreateVersion7();
        var changedAtUtc = DateTime.UtcNow.AddHours(-4);
        var referral = new CareConnectReferralLookupResult(
            referralId,
            "New",
            "Urgent",
            "Atlas Medical",
            "Jane Doe",
            "Physical Therapy",
            "Orthopedics",
            "Acme Law",
            "Pat Referrer",
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow,
            [new CareConnectReferralHistoryLookupItem("New", "NewOpened", changedAtUtc, "Opened by provider")]);
        var source = new FakeCareConnectAssistantSource(
            new CareConnectReferralLookupOutcome(
                true,
                "completed",
                null,
                referral));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            source,
            BuildHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "careconnect.referral.lookup",
            "generic",
            $$"""{"referralId":"{{referralId}}"}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.Equal("completed", result.Status);
        Assert.Contains("Atlas Medical", result.OutputJson);
        Assert.DoesNotContain("caseNumber", result.OutputJson, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.Citations);
        Assert.Equal("careconnect.referral", result.Citations[0].SourceType);
        Assert.Equal(referralId.ToString(), result.Citations[0].SourceId);
        Assert.Equal($"/careconnect/referrals/{referralId}", result.Citations[0].Url);
    }

    [Fact]
    public async Task ExecuteAsync_CareConnectLookup_WithInvalidInput_ReturnsInvalidInput()
    {
        var source = new FakeCareConnectAssistantSource(
            new CareConnectReferralLookupOutcome(false, "should_not_run", null, null));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            source,
            BuildHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "careconnect.referral.lookup",
            "generic",
            """{"referralId":"not-a-guid"}""",
            "{}"));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_input", result.Status);
        Assert.Equal("The CareConnect referral id is missing or invalid.", result.SafeError);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task ExecuteAsync_CareConnectReferralSearch_ReturnsSearchResultsAndCitations()
    {
        var source = new FakeCareConnectAssistantSource(
            new CareConnectReferralLookupOutcome(false, "unused", null, null),
            referralSearch: new CareConnectReferralSearchOutcome(
                true,
                "completed",
                null,
                2,
                [
                    new CareConnectReferralSearchResult(
                        Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        "Jane Doe",
                        "New",
                        "Urgent",
                        "Atlas Medical",
                        "Physical Therapy",
                        "Orthopedics",
                        "Acme Law",
                        "Pat Referrer",
                        DateTime.UtcNow.AddDays(-2),
                        DateTime.UtcNow),
                ]));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            source,
            BuildHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "careconnect.referral.search",
            "generic",
            """{"searchText":"Jane Doe","top":5}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.Contains("Jane Doe", result.OutputJson);
        Assert.DoesNotContain("caseNumber", result.OutputJson, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.Citations);
        Assert.Equal("careconnect.referral", result.Citations[0].SourceType);
    }

    [Fact]
    public async Task ExecuteAsync_CareConnectProviderSearch_ReturnsProviderResultsAndCitations()
    {
        var source = new FakeCareConnectAssistantSource(
            new CareConnectReferralLookupOutcome(false, "unused", null, null),
            providerSearch: new CareConnectProviderSearchOutcome(
                true,
                "completed",
                null,
                1,
                [
                    new CareConnectProviderSearchResult(
                        Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        "Atlas Medical",
                        "Atlas Health",
                        "Phoenix",
                        "AZ",
                        true,
                        true,
                        "Orthopedics",
                        "Atlas Medical"),
                ]));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            source,
            BuildHttpContextAccessor(withProviderSearch: true));

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "careconnect.provider.search",
            "generic",
            """{"name":"Atlas"}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.Contains("Atlas Medical", result.OutputJson);
        Assert.Single(result.Citations);
        Assert.Equal("careconnect.provider", result.Citations[0].SourceType);
    }

    [Fact]
    public async Task ExecuteAsync_CareConnectReferralSearch_MapsAliasFields()
    {
        var source = new FakeCareConnectAssistantSource(
            new CareConnectReferralLookupOutcome(false, "unused", null, null));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            source,
            BuildHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "careconnect.referral.search",
            "generic",
            """{"patientName":"Jane Doe","providerOrganizationName":"Atlas Health","lawFirmName":"Acme Law","top":5}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.NotNull(source.LastReferralSearchRequest);
        Assert.Equal("Jane Doe", source.LastReferralSearchRequest!.ClientName);
        Assert.Equal("Atlas Health", source.LastReferralSearchRequest.ProviderName);
        Assert.Equal("Acme Law", source.LastReferralSearchRequest.ReferrerName);
    }

    [Fact]
    public async Task ExecuteAsync_CareConnectReferralSearch_RemovesDirectiveWordsFromStructuredSearchText()
    {
        var source = new FakeCareConnectAssistantSource(
            new CareConnectReferralLookupOutcome(false, "unused", null, null));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            source,
            BuildHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "careconnect.referral.search",
            "generic",
            """{"searchText":"look for the latest referral sent to RL Medical Group","providerOrganizationName":"RL Medical Group","top":5}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.NotNull(source.LastReferralSearchRequest);
        Assert.Null(source.LastReferralSearchRequest!.SearchText);
        Assert.Equal("RL Medical Group", source.LastReferralSearchRequest.ProviderName);
    }

    private static IHttpContextAccessor BuildHttpContextAccessor(bool withProviderSearch = false)
    {
        var claims = new List<Claim>
        {
            new Claim("permissions", "SYNQ_CARECONNECT.referral:read:own"),
            new Claim("product_codes", "SYNQ_CARECONNECT"),
        };
        if (withProviderSearch)
            claims.Add(new Claim("permissions", "SYNQ_CARECONNECT.provider:search"));

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        };

        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class FakeCareConnectAssistantSource : ICareConnectAssistantSource
    {
        private readonly CareConnectReferralLookupOutcome _outcome;
        private readonly CareConnectReferralSearchOutcome _referralSearch;
        private readonly CareConnectProviderSearchOutcome _providerSearch;
        public CareConnectReferralSearchRequest? LastReferralSearchRequest { get; private set; }

        public FakeCareConnectAssistantSource(
            CareConnectReferralLookupOutcome outcome,
            CareConnectReferralSearchOutcome? referralSearch = null,
            CareConnectProviderSearchOutcome? providerSearch = null)
        {
            _outcome = outcome;
            _referralSearch = referralSearch ?? new CareConnectReferralSearchOutcome(true, "completed", null, 0, []);
            _providerSearch = providerSearch ?? new CareConnectProviderSearchOutcome(true, "completed", null, 0, []);
        }

        public Task<CareConnectReferralLookupOutcome> LookupReferralAsync(Guid referralId, CancellationToken ct = default)
            => Task.FromResult(_outcome);

        public Task<CareConnectReferralHistoryLookupOutcome> LookupReferralHistoryAsync(Guid referralId, int top, CancellationToken ct = default)
            => Task.FromResult(new CareConnectReferralHistoryLookupOutcome(
                true,
                "completed",
                null,
                new CareConnectReferralHistoryLookupResult(
                    referralId,
                    "Jane Doe",
                    "Atlas Medical",
                    "New",
                    [])));

        public Task<CareConnectReferralSearchOutcome> SearchReferralsAsync(CareConnectReferralSearchRequest request, CancellationToken ct = default)
        {
            LastReferralSearchRequest = request;
            return Task.FromResult(_referralSearch);
        }

        public Task<CareConnectProviderSearchOutcome> SearchProvidersAsync(CareConnectProviderSearchRequest request, CancellationToken ct = default)
            => Task.FromResult(_providerSearch);

        public Task<CareConnectReferrerSearchOutcome> SearchReferrersAsync(CareConnectReferrerSearchRequest request, CancellationToken ct = default)
            => Task.FromResult(new CareConnectReferrerSearchOutcome(true, "completed", null, 0, []));

        public Task<CareConnectReferralQueueSummaryOutcome> GetReferralQueueSummaryAsync(CareConnectReferralQueueSummaryRequest request, CancellationToken ct = default)
            => Task.FromResult(new CareConnectReferralQueueSummaryOutcome(true, "completed", null, 0, [], []));
    }
}
