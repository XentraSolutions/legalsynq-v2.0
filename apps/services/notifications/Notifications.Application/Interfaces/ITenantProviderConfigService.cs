using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface ITenantProviderConfigService
{
    Task<TenantProviderConfigDto?> GetByIdAsync(Guid tenantId, Guid id);
    Task<List<TenantProviderConfigDto>> ListAsync(Guid tenantId, string? channel = null);
    Task<TenantProviderConfigDto> CreateAsync(Guid tenantId, CreateTenantProviderConfigDto request);
    Task<TenantProviderConfigDto> UpdateAsync(Guid tenantId, Guid id, UpdateTenantProviderConfigDto request);
    Task DeleteAsync(Guid tenantId, Guid id);
    Task<TenantProviderConfigDto> ValidateAsync(Guid tenantId, Guid id);
    Task<TenantProviderConfigDto> HealthCheckAsync(Guid tenantId, Guid id);
}
