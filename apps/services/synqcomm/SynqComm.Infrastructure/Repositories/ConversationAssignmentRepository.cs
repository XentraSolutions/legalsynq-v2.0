using Microsoft.EntityFrameworkCore;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Infrastructure.Persistence;

namespace SynqComm.Infrastructure.Repositories;

public class ConversationAssignmentRepository : IConversationAssignmentRepository
{
    private readonly SynqCommDbContext _db;

    public ConversationAssignmentRepository(SynqCommDbContext db) => _db = db;

    public async Task<ConversationAssignment?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
        => await _db.ConversationAssignments
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.ConversationId == conversationId, ct);

    public async Task<List<ConversationAssignment>> ListByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default)
        => await _db.ConversationAssignments
            .Where(a => a.TenantId == tenantId && a.QueueId == queueId)
            .OrderByDescending(a => a.LastAssignedAtUtc)
            .ToListAsync(ct);

    public async Task<List<ConversationAssignment>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.ConversationAssignments
            .Where(a => a.TenantId == tenantId && a.AssignedUserId == userId)
            .OrderByDescending(a => a.LastAssignedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(ConversationAssignment assignment, CancellationToken ct = default)
    {
        _db.ConversationAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ConversationAssignment assignment, CancellationToken ct = default)
    {
        _db.ConversationAssignments.Update(assignment);
        await _db.SaveChangesAsync(ct);
    }
}
