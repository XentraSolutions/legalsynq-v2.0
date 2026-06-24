using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface ICareConnectAccessCodeService
{
    Task<CareConnectAccessCodeMetadataResponse> GetMetadataAsync(Guid tenantId, CancellationToken ct = default);
    Task<SetCareConnectAccessCodeResponse> SetAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task<CareConnectAccessCodeMetadataResponse> ClearAsync(Guid tenantId, CancellationToken ct = default);
    Task<CareConnectAccessCodeStatusResponse> GetStatusAsync(Guid tenantId, CancellationToken ct = default);
    Task<VerifyCareConnectAccessCodeResponse> VerifyAsync(Guid tenantId, string code, CancellationToken ct = default);
}
