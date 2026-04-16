using Microsoft.EntityFrameworkCore;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Infrastructure.Persistence;

namespace SynqComm.Infrastructure.Repositories;

public class ConversationSlaStateRepository : IConversationSlaStateRepository
{
    private readonly SynqCommDbContext _db;

    public ConversationSlaStateRepository(SynqCommDbContext db) => _db = db;

    public async Task<ConversationSlaState?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
        => await _db.ConversationSlaStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ConversationId == conversationId, ct);

    public async Task AddAsync(ConversationSlaState slaState, CancellationToken ct = default)
    {
        _db.ConversationSlaStates.Add(slaState);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationSlaState slaState, CancellationToken ct = default)
    {
        _db.ConversationSlaStates.Update(slaState);
        await _db.SaveChangesAsync(ct);
    }
}
