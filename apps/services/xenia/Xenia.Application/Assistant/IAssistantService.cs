namespace Xenia.Application.Assistant;

public interface IAssistantService
{
    Task<AssistantBootstrapDto> GetBootstrapAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssistantAgentDto>> ListAgentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssistantConversationSummaryDto>> ListConversationsAsync(CancellationToken ct = default);
    Task<AssistantConversationDto> CreateConversationAsync(CreateAssistantConversationRequest request, CancellationToken ct = default);
    Task<AssistantConversationDto?> GetConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task<AssistantConversationDto?> UpdateConversationAsync(Guid conversationId, UpdateAssistantConversationRequest request, CancellationToken ct = default);
    Task<bool> ArchiveConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task<AssistantMessageDto> CreateMessageAsync(Guid conversationId, CreateAssistantMessageRequest request, CancellationToken ct = default);
    IAsyncEnumerable<AssistantStreamEventDto> StreamMessageAsync(Guid conversationId, CreateAssistantMessageRequest request, CancellationToken ct = default);
    Task<AssistantUserPreferenceDto> GetPreferencesAsync(CancellationToken ct = default);
    Task<AssistantUserPreferenceDto> UpdatePreferencesAsync(UpdateAssistantPreferencesRequest request, CancellationToken ct = default);
}
