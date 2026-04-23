using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface IEntitlementService
{
    Task<List<EntitlementResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<EntitlementResponse>       GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<EntitlementResponse>       CreateAsync(Guid tenantId, CreateEntitlementRequest request, CancellationToken ct = default);
    Task<EntitlementResponse>       UpdateAsync(Guid tenantId, Guid id, UpdateEntitlementRequest request, CancellationToken ct = default);
    Task                            DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Sets the specified entitlement as the tenant default.
    /// Auto-demotes any prior default entitlement.
    /// The target entitlement must be enabled.
    /// </summary>
    Task<EntitlementResponse>       SetDefaultAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
