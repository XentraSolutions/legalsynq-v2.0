using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SynqComm.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly SynqCommDbContext _db;

    public ConversationRepository(SynqCommDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Conversation>> ListByContextAsync(Guid tenantId, string contextType, string contextId, CancellationToken ct = default)
    {
        return await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.ContextType == contextType && c.ContextId == contextId)
            .OrderByDescending(c => c.LastActivityAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Conversation entity, CancellationToken ct = default)
    {
        await _db.Conversations.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Conversation entity, CancellationToken ct = default)
    {
        _db.Conversations.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
