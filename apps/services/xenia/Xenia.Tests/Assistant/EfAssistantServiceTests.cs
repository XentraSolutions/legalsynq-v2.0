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
            new StaticAssistantToolRegistry(),
            _toolExecutor,
            _provider,
            new StaticRuntimeSettingsService(),
            Options.Create(new XeniaAssistantOptions
            {
                Provider = "Fake",
                ModelKey = "xenia-fake",
                MaxConversationMessages = 10,
                MaxPromptCharacters = 8000,
                MaxToolIterations = 4,
            }),
            _metrics,
            NullLogger<EfAssistantService>.Instance);

        var conversation = await sut.CreateConversationAsync(new CreateAssistantConversationRequest(
            "generic",
            null,
            "drawer",
            $$"""{"path":"/careconnect/referrals/{{referralId}}","source":"drawer"}"""));

        var events = await CollectAsync(sut.StreamMessageAsync(
            conversation.Id,
            new CreateAssistantMessageRequest("What is the status of this referral?", null, null)));

        Assert.NotNull(_toolExecutor.LastRequest);
        Assert.Equal("careconnect.referral.lookup", _toolExecutor.LastRequest!.ToolKey);
        Assert.Contains(referralId.ToString(), _toolExecutor.LastRequest.InputJson, StringComparison.Ordinal);

        Assert.Equal(3, _provider.Requests.Count);
        Assert.Equal(AssistantProviderPurpose.ToolSelection, _provider.Requests[0].Purpose);
        Assert.Equal(AssistantProviderPurpose.ToolSelection, _provider.Requests[1].Purpose);
        Assert.Equal(AssistantProviderPurpose.Chat, _provider.Requests[2].Purpose);
        Assert.Contains(
            _provider.Requests[1].Messages,
            message => message.Role == "tool" && message.Content.Contains("careconnect.referral.lookup", StringComparison.Ordinal));
        Assert.Contains(events, evt => evt.Type == "delta" && evt.Delta == "Grounded ");
        Assert.Contains(events, evt => evt.Type == "delta" && evt.Delta == "answer.");

        var refreshed = await sut.GetConversationAsync(conversation.Id);
        Assert.NotNull(refreshed);
        var assistantMessage = Assert.Single(refreshed!.Messages.Where(message => message.Role == "assistant"));
        Assert.Equal("Grounded answer.", assistantMessage.Content);
        Assert.Single(assistantMessage.Citations);
        Assert.Equal("careconnect.referral", assistantMessage.Citations[0].SourceType);
        Assert.Contains("lookupResults", assistantMessage.MetadataJson, StringComparison.Ordinal);

        var invocation = Assert.Single(_db.AssistantToolInvocations);
        Assert.Equal("completed", invocation.Status);
        Assert.Single(_db.AssistantMessages.Where(message => message.Role == AssistantMessageRole.Tool));
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

    private static async Task<List<AssistantStreamEventDto>> CollectAsync(IAsyncEnumerable<AssistantStreamEventDto> stream)
    {
        var events = new List<AssistantStreamEventDto>();
        await foreach (var evt in stream)
        {
            events.Add(evt);
        }

        return events;
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
        public List<AssistantProviderRequest> Requests { get; } = [];

        public Task<string> GetProviderKeyAsync(CancellationToken ct = default)
            => Task.FromResult("fake");

        public async IAsyncEnumerable<AssistantProviderEvent> StreamAsync(
            AssistantProviderRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Requests.Add(request);
            await Task.Yield();

            if (request.Purpose == AssistantProviderPurpose.ToolSelection &&
                !request.Messages.Any(message => message.Role == "tool"))
            {
                var referralId = ExtractReferralId(request.SystemPrompt) ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
                yield return new AssistantProviderEvent(
                    "delta",
                    Delta: $"{{\"action\":\"tool\",\"toolKey\":\"careconnect.referral.lookup\",\"input\":{{\"referralId\":\"{referralId}\"}}}}");
                yield return new AssistantProviderEvent(
                    "completed",
                    ProviderResponseId: "resp-plan-1",
                    InputTokens: 12,
                    OutputTokens: 12,
                    FinishReason: "stop");
                yield break;
            }

            if (request.Purpose == AssistantProviderPurpose.ToolSelection)
            {
                yield return new AssistantProviderEvent("delta", Delta: """{"action":"final","message":"Grounded answer."}""");
                yield return new AssistantProviderEvent(
                    "completed",
                    ProviderResponseId: "resp-plan-2",
                    InputTokens: 12,
                    OutputTokens: 4,
                    FinishReason: "stop");
                yield break;
            }

            yield return new AssistantProviderEvent("delta", Delta: "Grounded ");
            yield return new AssistantProviderEvent("delta", Delta: "answer.");
            yield return new AssistantProviderEvent(
                "completed",
                ProviderResponseId: "resp-chat-1",
                InputTokens: 12,
                OutputTokens: 4,
                FinishReason: "stop");
        }

        private static Guid? ExtractReferralId(string value)
        {
            var marker = "\"referralId\":\"";
            var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            var start = index + marker.Length;
            var end = value.IndexOf('"', start);
            if (end <= start) return null;

            return Guid.TryParse(value[start..end], out var parsed) ? parsed : null;
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
