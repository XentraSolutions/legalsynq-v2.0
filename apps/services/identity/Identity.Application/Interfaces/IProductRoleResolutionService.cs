using Identity.Application.DTOs;

namespace Identity.Application.Interfaces;

public interface IProductRoleResolutionService
{
    Task<EffectiveAccessContext> ResolveAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default);
}
