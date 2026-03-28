using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralAttachmentService
{
    Task<List<AttachmentMetadataResponse>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task<AttachmentMetadataResponse> CreateAsync(Guid tenantId, Guid referralId, Guid? userId, CreateAttachmentMetadataRequest request, CancellationToken ct = default);
}
