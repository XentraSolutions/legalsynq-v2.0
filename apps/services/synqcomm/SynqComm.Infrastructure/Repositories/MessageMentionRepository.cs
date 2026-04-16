using Microsoft.EntityFrameworkCore;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Infrastructure.Persistence;

namespace SynqComm.Infrastructure.Repositories;

public class MessageMentionRepository : IMessageMentionRepository
{
    private readonly SynqCommDbContext _db;

    public MessageMentionRepository(SynqCommDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<MessageMention> mentions, CancellationToken ct = default)
    {
        _db.MessageMentions.AddRange(mentions);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<MessageMention>> ListByMessageAsync(Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        return await _db.MessageMentions
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MessageId == messageId)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<MessageMention>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.MessageMentions
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<MessageMention>> ListByMentionedUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        return await _db.MessageMentions
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MentionedUserId == userId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
