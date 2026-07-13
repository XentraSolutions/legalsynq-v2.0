using System.Runtime.CompilerServices;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class FakeAssistantProvider : IAssistantProvider
{
    public Task<string> GetProviderKeyAsync(CancellationToken ct = default)
        => Task.FromResult("fake");

    public async IAsyncEnumerable<AssistantProviderEvent> StreamAsync(
        AssistantProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content
            ?? "How can I help?";

        var text =
            $"Xenia {request.AgentKey} is running in fake-provider mode. " +
            $"I received: {lastUserMessage.Trim()}";

        foreach (var chunk in Chunk(text, 24))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(15, ct);
            yield return new AssistantProviderEvent("delta", Delta: chunk);
        }

        yield return new AssistantProviderEvent(
            "completed",
            ProviderResponseId: $"fake-{Guid.CreateVersion7()}",
            InputTokens: EstimateTokens(request.Messages.Sum(m => m.Content.Length) + request.SystemPrompt.Length),
            OutputTokens: EstimateTokens(text.Length),
            FinishReason: "stop");
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        for (var i = 0; i < value.Length; i += size)
            yield return value.Substring(i, Math.Min(size, value.Length - i));
    }

    private static int EstimateTokens(int characters)
        => Math.Max(1, (int)Math.Ceiling(characters / 4.0));
}
