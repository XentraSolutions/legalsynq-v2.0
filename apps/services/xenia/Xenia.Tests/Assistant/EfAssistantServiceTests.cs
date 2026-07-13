using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;
using Xenia.Application.TenantContext;
using Xenia.Domain.Assistant;
using Xenia.Infrastructure.Assistant;
using Xenia.Infrastructure.Observability;
using Xenia.Infrastructure.Persistence;
using Xunit;

namespace Xenia.Tests.Assistant;

public sealed class EfAssistantServiceTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly XeniaTenantContextAccessor _tenantAccessor = new();
    private readonly RecordingToolExecutor _toolExecutor = new();
    private readonly RecordingAssistantProvider _provider = new();
    private readonly XeniaMetrics _metrics = new(new TestMeterFactory());

    public EfAssistantServiceTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new XeniaDbContext(options);
    }

    [Fact]
    public async Task StreamMessageAsync_OnCareConnectReferralRoute_PersistsToolInvocationAndCitations()
    {
        var tenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var referralId = Guid.CreateVersion7();

        SeedGenericAgent();
        _tenantAccessor.Set(new FakeTenantContext(tenantId, actorId));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "Bearer"))
        };

        var sut = new EfAssistantService(
            _db,
            _tenantAccessor,
            new HttpContextAccessor { HttpContext = httpContext },
            _toolExecutor,
            _provider,
            new StaticRuntimeSettingsService(),
            Options.Create(new XeniaAssistantOptions
            {
                Provider = "Fake",
                ModelKey = "xenia-fake",
                MaxConversationMessages = 10,
                MaxPromptCharacters = 8000,
            }),
            _metrics,
            NullLogger<EfAssistantService>.Instance);

        var conversation = await sut.CreateConversationAsync(new CreateAssistantConversationRequest(
            "generic",
            null,
            "drawer",
            $$"""{"path":"/careconnect/referrals/{{referralId}}","source":"drawer"}"""));

        await ConsumeAsync(sut.StreamMessageAsync(
            conversation.Id,
            new CreateAssistantMessageRequest("What is the status of this referral?", null, null)));

        Assert.NotNull(_toolExecutor.LastRequest);
        Assert.Equal("careconnect.referral.lookup", _toolExecutor.LastRequest!.ToolKey);
        Assert.Contains(referralId.ToString(), _toolExecutor.LastRequest.InputJson, StringComparison.Ordinal);

        Assert.NotNull(_provider.LastRequest);
        Assert.Contains(
            _provider.LastRequest!.Messages,
            message => message.Content.StartsWith("Authorized CareConnect referral context:", StringComparison.Ordinal));

        var refreshed = await sut.GetConversationAsync(conversation.Id);
        Assert.NotNull(refreshed);
        var assistantMessage = Assert.Single(refreshed!.Messages.Where(message => message.Role == "assistant"));
        Assert.Equal("Grounded answer.", assistantMessage.Content);
        Assert.Single(assistantMessage.Citations);
        Assert.Equal("careconnect.referral", assistantMessage.Citations[0].SourceType);

        var invocation = Assert.Single(_db.AssistantToolInvocations);
        Assert.Equal("completed", invocation.Status);
    }

    private void SeedGenericAgent()
    {
        _db.AssistantAgents.Add(new AssistantAgent(
            Guid.CreateVersion7(),
            "generic",
            "Generic Assistant",
            "General LegalSynq assistant for product-neutral questions and drafting.",
            "1.0.0",
            "You are Xenia.",
            """["tenant.context.summary","careconnect.referral.lookup"]""",
            "[]",
            isEnabled: true));
        _db.SaveChanges();
    }

    private static async Task ConsumeAsync(IAsyncEnumerable<AssistantStreamEventDto> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }

    public void Dispose()
    {
        _metrics.Dispose();
        _db.Dispose();
    }

    private sealed class FakeTenantContext : IXeniaTenantContext
    {
        public FakeTenantContext(Guid tenantId, Guid actorId)
        {
            TenantId = tenantId;
            ActorId = actorId;
        }

        public bool IsResolved => true;
        public Guid TenantId { get; }
        public string? TenantCode => "ACME";
        public Guid? ActorId { get; }
        public string? CorrelationId => "test-correlation";
    }

    private sealed class RecordingToolExecutor : IAssistantToolExecutor
    {
        public AssistantToolExecutionRequestDto? LastRequest { get; private set; }

        public Task<AssistantToolExecutionResultDto> ExecuteAsync(
            AssistantToolExecutionRequestDto request,
            CancellationToken ct = default)
        {
            LastRequest = request;
            var referralId = ExtractReferralId(request.InputJson);
            var outputJson = JsonSerializer.Serialize(new
            {
                status = "available",
                referral = new
                {
                    id = referralId,
                    status = "New",
                    providerName = "Atlas Medical",
                }
            });

            return Task.FromResult(new AssistantToolExecutionResultDto(
                true,
                "completed",
                outputJson,
                null,
                outputJson.Length,
                [
                    new AssistantToolCitationDto(
                        "careconnect.referral",
                        referralId,
                        "CareConnect referral Jane Doe",
                        $"/careconnect/referrals/{referralId}")
                ]));
        }

        private static string ExtractReferralId(string inputJson)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(inputJson);
            return doc.RootElement.GetProperty("referralId").GetString()
                ?? throw new InvalidOperationException("Missing referralId.");
        }
    }

    private sealed class RecordingAssistantProvider : IAssistantProvider
    {
        public AssistantProviderRequest? LastRequest { get; private set; }

        public Task<string> GetProviderKeyAsync(CancellationToken ct = default)
            => Task.FromResult("fake");

        public async IAsyncEnumerable<AssistantProviderEvent> StreamAsync(
            AssistantProviderRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            LastRequest = request;
            await Task.Yield();
            yield return new AssistantProviderEvent("delta", Delta: "Grounded answer.");
            yield return new AssistantProviderEvent(
                "completed",
                ProviderResponseId: "resp-1",
                InputTokens: 12,
                OutputTokens: 4,
                FinishReason: "stop");
        }
    }

    private sealed class StaticRuntimeSettingsService : IAssistantRuntimeSettingsService
    {
        public Task<AssistantRuntimeSettings> GetEffectiveSettingsAsync(Guid? tenantId, CancellationToken ct = default)
            => Task.FromResult(new AssistantRuntimeSettings(
                "Fake",
                "xenia-fake",
                "https://api.openai.com",
                null,
                60,
                null,
                null,
                null,
                null));
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options)
            => new(options.Name, options.Version, options.Tags);

        public void Dispose()
        {
        }
    }
}
