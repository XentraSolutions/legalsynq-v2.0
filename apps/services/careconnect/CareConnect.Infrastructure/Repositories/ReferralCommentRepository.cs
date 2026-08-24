using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralCommentRepository : IReferralCommentRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralCommentRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public Task<List<ReferralComment>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default) =>
        _db.ReferralComments
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ReferralId == referralId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ReferralComment comment, CancellationToken ct = default)
    {
        await _db.ReferralComments.AddAsync(comment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddWithAttachmentsAsync(
        ReferralComment comment,
        IReadOnlyCollection<ReferralAttachment> attachments,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.ReferralComments.AddAsync(comment, ct);
        if (attachments.Count > 0)
            await _db.ReferralAttachments.AddRangeAsync(attachments, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
