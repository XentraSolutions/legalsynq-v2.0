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
            "CC-123",
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

    private static IHttpContextAccessor BuildHttpContextAccessor()
    {
        var claims = new[]
        {
            new Claim("permissions", "SYNQ_CARECONNECT.referral:read:own"),
            new Claim("product_codes", "SYNQ_CARECONNECT"),
        };

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        };

        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class FakeCareConnectAssistantSource : ICareConnectAssistantSource
    {
        private readonly CareConnectReferralLookupOutcome _outcome;

        public FakeCareConnectAssistantSource(CareConnectReferralLookupOutcome outcome)
            => _outcome = outcome;

        public Task<CareConnectReferralLookupOutcome> LookupReferralAsync(Guid referralId, CancellationToken ct = default)
            => Task.FromResult(_outcome);
    }
}
