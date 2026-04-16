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

    public async Task<Message?> GetByIdAsync(Guid tenantId, Guid conversationId, Guid messageId, CancellationToken ct = default)
    {
        return await _db.Messages
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId && m.Id == messageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Message>> ListByConversationOrderedAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.Messages
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .OrderBy(m => m.SentAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task<Message?> GetLatestByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.Messages
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAtUtc)
            .ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Message entity, CancellationToken ct = default)
    {
        await _db.Messages.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }
}
