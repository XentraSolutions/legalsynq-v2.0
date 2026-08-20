using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface ITenantRegistrationService
{
    Task<SubmitTenantRegistrationResponse> SubmitAsync(SubmitTenantRegistrationRequest request, CancellationToken ct = default);
    Task<TenantRegistrationResponse?> GetAsync(Guid id, CancellationToken ct = default);
    Task<TenantRegistrationListResponse> ListAsync(string? registrationStatus, string? provisioningStatus,
        string? search, DateTime? submittedFrom, DateTime? submittedTo, int page, int pageSize, CancellationToken ct = default);
    Task<TenantRegistrationDecisionResponse> ApproveAsync(Guid id, Guid reviewerId, CancellationToken ct = default);
    Task<TenantRegistrationResponse> DeclineAsync(Guid id, Guid reviewerId, string reason, CancellationToken ct = default);
    Task<TenantRegistrationDecisionResponse> RetryProvisioningAsync(Guid id, CancellationToken ct = default);
}

