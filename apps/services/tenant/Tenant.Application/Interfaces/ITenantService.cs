using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface ITenantService
{
    Task<TenantResponse?>                           GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantResponse?>                           GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(List<TenantResponse> Items, int Total)>   ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<TenantResponse>                            CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantResponse>                            UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default);
    Task                                            DeactivateAsync(Guid id, CancellationToken ct = default);
}
