using System.Text.Json;
using Xenia.Application.Assistant;
using Xenia.Infrastructure.Assistant;
using Xunit;

namespace Xenia.Tests.Assistant;

public sealed class FakeAssistantProviderTests
{
    [Fact]
    public async Task StreamAsync_ToolSelection_UsesReferralSearch_ForNaturalLanguageReferralLookup()
    {
        var provider = new FakeAssistantProvider();
        var request = new AssistantProviderRequest(
            AgentKey: "careconnect",
            AgentVersion: "1.0.0",
            SystemPrompt: "system",
            ModelKey: "fake",
            Messages:
            [
                new AssistantProviderMessage(
                    "user",
                    "Find the referral for Jane Doe at Atlas Health from Acme Law")
            ],
            ContextJson: "{}",
            CorrelationId: "corr-1",
            Purpose: AssistantProviderPurpose.ToolSelection);

        var json = await CollectTextAsync(provider, request);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("tool", doc.RootElement.GetProperty("action").GetString());
        Assert.Equal("careconnect.referral.search", doc.RootElement.GetProperty("toolKey").GetString());
        Assert.Equal(
            "Find the referral for Jane Doe at Atlas Health from Acme Law",
            doc.RootElement.GetProperty("input").GetProperty("searchText").GetString());
    }

    private static async Task<string> CollectTextAsync(IAssistantProvider provider, AssistantProviderRequest request)
    {
        var chunks = new List<string>();

        await foreach (var evt in provider.StreamAsync(request))
        {
            if (evt.Type == "delta" && evt.Delta is not null)
                chunks.Add(evt.Delta);
        }

        return string.Concat(chunks);
    }
}
