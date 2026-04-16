using Microsoft.EntityFrameworkCore;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Domain.Enums;
using SynqComm.Infrastructure.Persistence;

namespace SynqComm.Infrastructure.Repositories;

public class EmailRecipientRecordRepository : IEmailRecipientRecordRepository
{
    private readonly SynqCommDbContext _db;
    public EmailRecipientRecordRepository(SynqCommDbContext db) => _db = db;

    public async Task AddAsync(EmailRecipientRecord record, CancellationToken ct = default)
    {
        await _db.EmailRecipientRecords.AddAsync(record, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<EmailRecipientRecord> records, CancellationToken ct = default)
    {
        await _db.EmailRecipientRecords.AddRangeAsync(records, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<EmailRecipientRecord>> ListByEmailReferenceAsync(
        Guid tenantId, Guid emailMessageReferenceId, CancellationToken ct = default)
    {
        return await _db.EmailRecipientRecords
            .Where(r => r.TenantId == tenantId && r.EmailMessageReferenceId == emailMessageReferenceId)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<EmailRecipientRecord>> ListVisibleByEmailReferenceAsync(
        Guid tenantId, Guid emailMessageReferenceId, CancellationToken ct = default)
    {
        return await _db.EmailRecipientRecords
            .Where(r => r.TenantId == tenantId
                && r.EmailMessageReferenceId == emailMessageReferenceId
                && r.RecipientVisibility == RecipientVisibility.Visible)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<EmailRecipientRecord>> ListVisibleByConversationAsync(
        Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        return await _db.EmailRecipientRecords
            .Where(r => r.TenantId == tenantId
                && r.ConversationId == conversationId
                && r.RecipientVisibility == RecipientVisibility.Visible)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        Guid tenantId, Guid emailMessageReferenceId, string normalizedEmail, CancellationToken ct = default)
    {
        return await _db.EmailRecipientRecords
            .AnyAsync(r => r.TenantId == tenantId
                && r.EmailMessageReferenceId == emailMessageReferenceId
                && r.NormalizedEmail == normalizedEmail, ct);
    }

    public async Task UpdateAsync(EmailRecipientRecord record, CancellationToken ct = default)
    {
        _db.EmailRecipientRecords.Update(record);
        await _db.SaveChangesAsync(ct);
    }
}
