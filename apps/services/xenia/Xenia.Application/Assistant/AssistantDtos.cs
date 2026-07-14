namespace Xenia.Application.Assistant;

public sealed record AssistantAgentDto(
    string AgentKey,
    string Name,
    string Description,
    string Version,
    bool Enabled,
    IReadOnlyList<string> AllowedTools,
    IReadOnlyList<string> RequiredProductCodes);

public sealed record AssistantBootstrapDto(
    bool Enabled,
    IReadOnlyList<AssistantAgentDto> Agents,
    AssistantUserPreferenceDto Preferences,
    AssistantUsageSummaryDto Usage,
    IReadOnlyDictionary<string, string> FeatureFlags);

public sealed record AssistantConversationSummaryDto(
    Guid Id,
    string AgentKey,
    string AgentVersion,
    string Title,
    string Source,
    string Status,
    DateTime? LastMessageAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AssistantConversationDto(
    Guid Id,
    string AgentKey,
    string AgentVersion,
    string Title,
    string Source,
    string Status,
    string ContextJson,
    DateTime? LastMessageAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<AssistantMessageDto> Messages);

public sealed record AssistantMessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    string Provider,
    string? ProviderResponseId,
    int? InputTokens,
    int? OutputTokens,
    string? FinishReason,
    DateTime CreatedAtUtc,
    string MetadataJson,
    IReadOnlyList<AssistantCitationDto> Citations);

public sealed record AssistantCitationDto(
    Guid Id,
    string SourceType,
    string SourceId,
    string Label,
    string? Url);

public sealed record AssistantUserPreferenceDto(
    string DefaultAgentKey,
    bool ContextHintsEnabled,
    string PreferencesJson);

public sealed record AssistantUsageSummaryDto(
    int RequestsThisMonth,
    int InputTokensThisMonth,
    int OutputTokensThisMonth,
    decimal EstimatedCostUsdThisMonth,
    int? MonthlyRequestLimit,
    int? MonthlyTokenLimit);

public sealed record CreateAssistantConversationRequest(
    string? AgentKey,
    string? Title,
    string? Source,
    string? ContextJson);

public sealed record UpdateAssistantConversationRequest(string? Title, bool? Archived);

public sealed record CreateAssistantMessageRequest(
    string Content,
    string? ContextJson,
    string? ClientMessageId);

public sealed record UpdateAssistantPreferencesRequest(
    string? DefaultAgentKey,
    bool? ContextHintsEnabled,
    string? PreferencesJson);

public sealed record AssistantStreamEventDto(
    string Type,
    string? Delta,
    AssistantMessageDto? Message,
    string? Error);
