using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SynqComm.Infrastructure.Repositories;

public class ConversationReadStateRepository : IConversationReadStateRepository
{
    private readonly SynqCommDbContext _db;

    public ConversationReadStateRepository(SynqCommDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationReadState?> GetAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        return await _db.ConversationReadStates
            .Where(r => r.TenantId == tenantId && r.ConversationId == conversationId && r.UserId == userId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ConversationReadState entity, CancellationToken ct = default)
    {
        await _db.ConversationReadStates.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationReadState entity, CancellationToken ct = default)
    {
        _db.ConversationReadStates.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
