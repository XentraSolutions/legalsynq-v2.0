using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralNoteService
{
    Task<List<ReferralNoteResponse>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task<ReferralNoteResponse> CreateAsync(Guid tenantId, Guid referralId, Guid? userId, CreateReferralNoteRequest request, CancellationToken ct = default);
    Task<ReferralNoteResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateReferralNoteRequest request, CancellationToken ct = default);
}
