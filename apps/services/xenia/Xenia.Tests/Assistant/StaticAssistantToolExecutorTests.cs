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
            new FakeSynqLienAssistantSource(),
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
            new FakeSynqLienAssistantSource(),
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
            new FakeSynqLienAssistantSource(),
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
            new FakeSynqLienAssistantSource(),
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
            new FakeSynqLienAssistantSource(),
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
            new FakeSynqLienAssistantSource(),
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

    [Fact]
    public async Task ExecuteAsync_CareConnectQueueSummary_MapsKpiFiltersAndReturnsSummary()
    {
        var referralId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var source = new FakeCareConnectAssistantSource(
            new CareConnectReferralLookupOutcome(false, "unused", null, null),
            queueSummary: new CareConnectReferralQueueSummaryOutcome(
                true,
                "completed",
                null,
                24,
                9,
                4,
                4,
                7,
                2,
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow,
                null,
                "new",
                [new CareConnectReferralQueueStatusCount("New", 3), new CareConnectReferralQueueStatusCount("NewOpened", 1)],
                [
                    new CareConnectReferralSearchResult(
                        referralId,
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
            new FakeSynqLienAssistantSource(),
            BuildHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "careconnect.referral.queue.summary",
            "careconnect",
            """{"searchText":"how many new referrals did I get in the last 7 days","statusGroup":"new","days":7,"recentTop":5}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.NotNull(source.LastQueueSummaryRequest);
        Assert.Equal("new", source.LastQueueSummaryRequest!.StatusGroup);
        Assert.Equal(7, source.LastQueueSummaryRequest.Days);
        Assert.Null(source.LastQueueSummaryRequest.Status);
        Assert.Null(source.LastQueueSummaryRequest.SearchText);
        Assert.Contains("matchingReferralCount", result.OutputJson);
        Assert.Contains("newReferralCount", result.OutputJson);
        Assert.Single(result.Citations);
        Assert.Equal(referralId.ToString(), result.Citations[0].SourceId);
    }

    [Fact]
    public async Task ExecuteAsync_SynqLienLookup_ReturnsGroundedLienOutputAndCitation()
    {
        var lienId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var synqLien = new FakeSynqLienAssistantSource(
            lienLookup: new SynqLienLienLookupOutcome(
                true,
                "completed",
                null,
                new SynqLienLienLookupResult(
                    lienId,
                    "LN-1001",
                    "Active",
                    "MedicalLien",
                    "Jane Doe",
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    "CASE-1001",
                    "Jane Doe v Example",
                    12500m,
                    9800m,
                    null,
                    null,
                    null,
                    "CA",
                    false,
                    DateTime.UtcNow.AddDays(-3),
                    DateTime.UtcNow)));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            new FakeCareConnectAssistantSource(new CareConnectReferralLookupOutcome(false, "unused", null, null)),
            synqLien,
            BuildSynqLienHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "synqlien.lien.lookup",
            "synqlien",
            $$"""{"lienId":"{{lienId}}"}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.Contains("LN-1001", result.OutputJson);
        Assert.Single(result.Citations);
        Assert.Equal("synqlien.lien", result.Citations[0].SourceType);
        Assert.Equal($"/lien/liens/{lienId}", result.Citations[0].Url);
    }

    [Fact]
    public async Task ExecuteAsync_SynqLienSearch_MapsAliasFieldsAndNormalizesFilters()
    {
        var synqLien = new FakeSynqLienAssistantSource();
        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            new FakeCareConnectAssistantSource(new CareConnectReferralLookupOutcome(false, "unused", null, null)),
            synqLien,
            BuildSynqLienHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "synqlien.lien.search",
            "synqlien",
            """{"searchText":"find open medical liens for Jane Doe","clientName":"Jane Doe","statusGroup":"open","lienType":"medical","top":5}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.NotNull(synqLien.LastLienSearchRequest);
        Assert.Equal("Jane Doe", synqLien.LastLienSearchRequest!.SubjectName);
        Assert.Equal("open", synqLien.LastLienSearchRequest.StatusGroup);
        Assert.Null(synqLien.LastLienSearchRequest.Status);
        Assert.Equal("MedicalLien", synqLien.LastLienSearchRequest.LienType);
        Assert.Null(synqLien.LastLienSearchRequest.SearchText);
    }

    [Fact]
    public async Task ExecuteAsync_SynqLienQueueSummary_MapsKpiFiltersAndReturnsSummary()
    {
        var lienId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var synqLien = new FakeSynqLienAssistantSource(
            queueSummary: new SynqLienQueueSummaryOutcome(
                true,
                "completed",
                null,
                42,
                12,
                8,
                3,
                9,
                2,
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                null,
                "open",
                [new SynqLienStatusCount("Draft", 3), new SynqLienStatusCount("Active", 6)],
                [
                    new SynqLienLienSearchResult(
                        lienId,
                        "LN-2001",
                        "Active",
                        "MedicalLien",
                        "Jane Doe",
                        null,
                        null,
                        15000m,
                        12000m,
                        DateTime.UtcNow.AddDays(-5),
                        DateTime.UtcNow),
                ]));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            new FakeCareConnectAssistantSource(new CareConnectReferralLookupOutcome(false, "unused", null, null)),
            synqLien,
            BuildSynqLienHttpContextAccessor());

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "synqlien.lien.queue.summary",
            "synqlien",
            """{"searchText":"how many open medical liens in the last month","statusGroup":"open","lienType":"medical","days":30,"recentTop":5}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.NotNull(synqLien.LastQueueSummaryRequest);
        Assert.Equal("open", synqLien.LastQueueSummaryRequest!.StatusGroup);
        Assert.Equal(30, synqLien.LastQueueSummaryRequest.Days);
        Assert.Equal("MedicalLien", synqLien.LastQueueSummaryRequest.LienType);
        Assert.Null(synqLien.LastQueueSummaryRequest.SearchText);
        Assert.Contains("matchingLienCount", result.OutputJson);
        Assert.Contains("openLienCount", result.OutputJson);
        Assert.Single(result.Citations);
        Assert.Equal(lienId.ToString(), result.Citations[0].SourceId);
    }

    [Fact]
    public async Task ExecuteAsync_SynqLienCaseLookup_ReturnsCaseAndCitation()
    {
        var caseId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var synqLien = new FakeSynqLienAssistantSource(
            caseLookup: new SynqLienCaseLookupOutcome(
                true,
                "completed",
                null,
                new SynqLienCaseLookupResult(
                    caseId,
                    "CASE-2001",
                    "Jane Doe",
                    "PreDemand",
                    "Jane Doe v Example",
                    "PI",
                    "Treating",
                    "Acme Law",
                    "Pat Manager",
                    50000m,
                    null,
                    DateTime.UtcNow.AddDays(-10),
                    DateTime.UtcNow,
                    [])));

        var sut = new StaticAssistantToolExecutor(
            new StaticAssistantToolRegistry(),
            new FakeCareConnectAssistantSource(new CareConnectReferralLookupOutcome(false, "unused", null, null)),
            synqLien,
            BuildSynqLienHttpContextAccessor(includeCaseRead: true));

        var result = await sut.ExecuteAsync(new AssistantToolExecutionRequestDto(
            "synqlien.case.lookup",
            "synqlien",
            $$"""{"caseId":"{{caseId}}"}""",
            "{}"));

        Assert.True(result.Succeeded);
        Assert.Contains("CASE-2001", result.OutputJson);
        Assert.Single(result.Citations);
        Assert.Equal("synqlien.case", result.Citations[0].SourceType);
        Assert.Equal($"/lien/cases/{caseId}", result.Citations[0].Url);
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

    private static IHttpContextAccessor BuildSynqLienHttpContextAccessor(bool includeCaseRead = false)
    {
        var claims = new List<Claim>
        {
            new Claim("permissions", "SYNQ_LIENS.lien:read"),
            new Claim("product_codes", "SYNQ_LIENS"),
        };
        if (includeCaseRead)
            claims.Add(new Claim("permissions", "SYNQ_LIENS.case:read"));

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
        private readonly CareConnectReferralQueueSummaryOutcome _queueSummary;
        public CareConnectReferralSearchRequest? LastReferralSearchRequest { get; private set; }
        public CareConnectReferralQueueSummaryRequest? LastQueueSummaryRequest { get; private set; }

        public FakeCareConnectAssistantSource(
            CareConnectReferralLookupOutcome outcome,
            CareConnectReferralSearchOutcome? referralSearch = null,
            CareConnectProviderSearchOutcome? providerSearch = null,
            CareConnectReferralQueueSummaryOutcome? queueSummary = null)
        {
            _outcome = outcome;
            _referralSearch = referralSearch ?? new CareConnectReferralSearchOutcome(true, "completed", null, 0, []);
            _providerSearch = providerSearch ?? new CareConnectProviderSearchOutcome(true, "completed", null, 0, []);
            _queueSummary = queueSummary ?? new CareConnectReferralQueueSummaryOutcome(true, "completed", null, 0, 0, 0, 0, 0, 0, null, null, null, null, [], []);
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
        {
            LastQueueSummaryRequest = request;
            return Task.FromResult(_queueSummary);
        }
    }

    private sealed class FakeSynqLienAssistantSource : ISynqLienAssistantSource
    {
        private readonly SynqLienLienLookupOutcome _lienLookup;
        private readonly SynqLienLienSearchOutcome _lienSearch;
        private readonly SynqLienQueueSummaryOutcome _queueSummary;
        private readonly SynqLienCaseLookupOutcome _caseLookup;
        private readonly SynqLienCaseSearchOutcome _caseSearch;

        public SynqLienLienSearchRequest? LastLienSearchRequest { get; private set; }
        public SynqLienQueueSummaryRequest? LastQueueSummaryRequest { get; private set; }

        public FakeSynqLienAssistantSource(
            SynqLienLienLookupOutcome? lienLookup = null,
            SynqLienLienSearchOutcome? lienSearch = null,
            SynqLienQueueSummaryOutcome? queueSummary = null,
            SynqLienCaseLookupOutcome? caseLookup = null,
            SynqLienCaseSearchOutcome? caseSearch = null)
        {
            _lienLookup = lienLookup ?? new SynqLienLienLookupOutcome(false, "not_found", "unused", null);
            _lienSearch = lienSearch ?? new SynqLienLienSearchOutcome(true, "completed", null, 0, []);
            _queueSummary = queueSummary ?? new SynqLienQueueSummaryOutcome(true, "completed", null, 0, 0, 0, 0, 0, 0, null, null, null, null, [], []);
            _caseLookup = caseLookup ?? new SynqLienCaseLookupOutcome(false, "not_found", "unused", null);
            _caseSearch = caseSearch ?? new SynqLienCaseSearchOutcome(true, "completed", null, 0, []);
        }

        public Task<SynqLienLienLookupOutcome> LookupLienAsync(SynqLienLienLookupRequest request, CancellationToken ct = default)
            => Task.FromResult(_lienLookup);

        public Task<SynqLienLienSearchOutcome> SearchLiensAsync(SynqLienLienSearchRequest request, CancellationToken ct = default)
        {
            LastLienSearchRequest = request;
            return Task.FromResult(_lienSearch);
        }

        public Task<SynqLienQueueSummaryOutcome> GetLienQueueSummaryAsync(SynqLienQueueSummaryRequest request, CancellationToken ct = default)
        {
            LastQueueSummaryRequest = request;
            return Task.FromResult(_queueSummary);
        }

        public Task<SynqLienCaseLookupOutcome> LookupCaseAsync(SynqLienCaseLookupRequest request, CancellationToken ct = default)
            => Task.FromResult(_caseLookup);

        public Task<SynqLienCaseSearchOutcome> SearchCasesAsync(SynqLienCaseSearchRequest request, CancellationToken ct = default)
            => Task.FromResult(_caseSearch);
    }
}
