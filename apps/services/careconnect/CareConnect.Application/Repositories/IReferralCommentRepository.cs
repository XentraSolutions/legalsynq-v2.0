using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralCommentRepository
{
    Task<List<ReferralComment>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task AddAsync(ReferralComment comment, CancellationToken ct = default);
    Task AddWithAttachmentsAsync(ReferralComment comment, IReadOnlyCollection<ReferralAttachment> attachments, CancellationToken ct = default);
}
