using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SynqComm.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly SynqCommDbContext _db;

    public MessageRepository(SynqCommDbContext db)
    {
        _db = db;
    }

    public async Task<List<Message>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.Messages
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .OrderBy(m => m.SentAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Message entity, CancellationToken ct = default)
    {
        await _db.Messages.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }
}
