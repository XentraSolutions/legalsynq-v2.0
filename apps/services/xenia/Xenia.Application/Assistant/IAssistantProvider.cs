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
    string CorrelationId,
    AssistantProviderPurpose Purpose = AssistantProviderPurpose.Chat);

public sealed record AssistantProviderMessage(string Role, string Content);

public enum AssistantProviderPurpose
{
    Chat = 0,
    ToolSelection = 1,
}

public sealed record AssistantProviderEvent(
    string Type,
    string? Delta = null,
    string? ProviderResponseId = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    string? FinishReason = null,
    string? SafeError = null);
