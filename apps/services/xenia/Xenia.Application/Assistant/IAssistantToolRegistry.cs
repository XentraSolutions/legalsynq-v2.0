namespace Xenia.Application.Assistant;

public interface IAssistantToolRegistry
{
    IReadOnlyList<AssistantToolDefinitionDto> ListToolsForAgent(string agentKey);
}

public interface IAssistantToolExecutor
{
    Task<AssistantToolExecutionResultDto> ExecuteAsync(
        AssistantToolExecutionRequestDto request,
        CancellationToken ct = default);
}

public sealed record AssistantToolDefinitionDto(
    string ToolKey,
    string Name,
    string Description,
    string InputSchemaJson,
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlyList<string> RequiredProductCodes,
    bool ConfirmationRequired,
    int MaxOutputCharacters);

public sealed record AssistantToolExecutionRequestDto(
    string ToolKey,
    string AgentKey,
    string InputJson,
    string ContextJson);

public sealed record AssistantToolCitationDto(
    string SourceType,
    string SourceId,
    string Label,
    string? Url);

public sealed record AssistantToolExecutionResultDto(
    bool Succeeded,
    string Status,
    string OutputJson,
    string? SafeError,
    int OutputCharacters,
    IReadOnlyList<AssistantToolCitationDto> Citations);
