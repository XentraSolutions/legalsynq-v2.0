using SynqComm.Domain.Entities;

namespace SynqComm.Application.Repositories;

public interface IParticipantRepository
{
    Task<List<ConversationParticipant>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<ConversationParticipant?> FindActiveAsync(Guid tenantId, Guid conversationId, Guid? userId, string? externalEmail, CancellationToken ct = default);
    Task AddAsync(ConversationParticipant entity, CancellationToken ct = default);
    Task UpdateAsync(ConversationParticipant entity, CancellationToken ct = default);
}
