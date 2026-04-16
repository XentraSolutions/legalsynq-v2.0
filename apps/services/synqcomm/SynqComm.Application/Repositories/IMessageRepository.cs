using SynqComm.Domain.Entities;

namespace SynqComm.Application.Repositories;

public interface IMessageRepository
{
    Task<List<Message>> ListByConversationOrderedAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<Message?> GetLatestByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task AddAsync(Message entity, CancellationToken ct = default);
}
