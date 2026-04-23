using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface IBrandingService
{
    Task<BrandingResponse>        GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<BrandingResponse>        UpsertAsync(Guid tenantId, UpdateBrandingRequest request, CancellationToken ct = default);
    Task<PublicBrandingResponse?> GetPublicByCodeAsync(string code, CancellationToken ct = default);
    Task<PublicBrandingResponse?> GetPublicBySubdomainAsync(string subdomain, CancellationToken ct = default);
}
