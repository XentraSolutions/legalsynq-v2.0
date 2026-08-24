using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralAttachmentRepository : IReferralAttachmentRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralAttachmentRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<ReferralAttachment>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
        => await _db.ReferralAttachments
            .Where(a => a.TenantId == tenantId && a.ReferralId == referralId && a.ReferralCommentId == null)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<List<ReferralAttachment>> GetByReferralIncludingMessageAttachmentsAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
        => await _db.ReferralAttachments
            .Where(a => a.TenantId == tenantId && a.ReferralId == referralId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<List<ReferralAttachment>> GetByReferralCommentIdsAsync(
        Guid tenantId,
        Guid referralId,
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken ct = default)
    {
        if (commentIds.Count == 0)
            return [];

        var ids = commentIds.ToList();
        return await _db.ReferralAttachments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId
                && a.ReferralId == referralId
                && a.ReferralCommentId.HasValue
                && ids.Contains(a.ReferralCommentId.Value))
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ReferralAttachment attachment, CancellationToken ct = default)
    {
        await _db.ReferralAttachments.AddAsync(attachment, ct);
        await _db.SaveChangesAsync(ct);
    }
}
