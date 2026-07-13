namespace Xenia.Application.Assistant;

public interface IAssistantProvider
{
    Task<string> GetProviderKeyAsync(CancellationToken ct = default);

    IAsyncEnumerable<AssistantProviderEvent> StreamAsync(
        AssistantProviderRequest request,
        CancellationToken ct = default);
}

public sealed record AssistantProviderRequest(
    string AgentKey,
    string AgentVersion,
    string SystemPrompt,
    string ModelKey,
    IReadOnlyList<AssistantProviderMessage> Messages,
    string ContextJson,
    string CorrelationId);

public sealed record AssistantProviderMessage(string Role, string Content);

public sealed record AssistantProviderEvent(
    string Type,
    string? Delta = null,
    string? ProviderResponseId = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    string? FinishReason = null,
    string? SafeError = null);
