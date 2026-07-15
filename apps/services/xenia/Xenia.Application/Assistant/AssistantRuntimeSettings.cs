namespace Xenia.Application.Assistant;

public sealed record AssistantRuntimeSettings(
    string Provider,
    string ModelKey,
    string OpenAiBaseUrl,
    string? OpenAiApiKey,
    int OpenAiTimeoutSeconds,
    string? OpenAiReasoningEffort,
    string? OpenAiTextVerbosity,
    int? OpenAiMaxOutputTokens,
    DateTime? LastUpdatedAtUtc)
{
    public bool HasOpenAiApiKey => !string.IsNullOrWhiteSpace(OpenAiApiKey);
}

public interface IAssistantRuntimeSettingsService
{
    Task<AssistantRuntimeSettings> GetEffectiveSettingsAsync(
        Guid? tenantId,
        CancellationToken ct = default);
}

public static class AssistantConfigurationKeys
{
    public const string Provider = "provider";
    public const string ModelKey = "modelKey";
    public const string OpenAiBaseUrl = "openAi.baseUrl";
    public const string OpenAiTimeoutSeconds = "openAi.timeoutSeconds";
    public const string OpenAiReasoningEffort = "openAi.reasoningEffort";
    public const string OpenAiTextVerbosity = "openAi.textVerbosity";
    public const string OpenAiMaxOutputTokens = "openAi.maxOutputTokens";

    public static IReadOnlyList<string> All { get; } =
    [
        Provider,
        ModelKey,
        OpenAiBaseUrl,
        OpenAiTimeoutSeconds,
        OpenAiReasoningEffort,
        OpenAiTextVerbosity,
        OpenAiMaxOutputTokens,
    ];
}
