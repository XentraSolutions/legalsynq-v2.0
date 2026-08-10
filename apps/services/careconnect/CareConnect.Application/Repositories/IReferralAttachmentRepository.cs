using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralAttachmentRepository
{
    Task<List<ReferralAttachment>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task<List<ReferralAttachment>> GetByReferralIncludingMessageAttachmentsAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task<List<ReferralAttachment>> GetByReferralCommentIdsAsync(Guid tenantId, Guid referralId, IReadOnlyCollection<Guid> commentIds, CancellationToken ct = default);
    Task AddAsync(ReferralAttachment attachment, CancellationToken ct = default);
}
