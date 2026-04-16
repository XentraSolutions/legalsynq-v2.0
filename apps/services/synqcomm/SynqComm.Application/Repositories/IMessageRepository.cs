using SynqComm.Domain.Entities;

namespace SynqComm.Application.Repositories;

public interface IMessageRepository
{
    Task<List<Message>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task AddAsync(Message entity, CancellationToken ct = default);
}
