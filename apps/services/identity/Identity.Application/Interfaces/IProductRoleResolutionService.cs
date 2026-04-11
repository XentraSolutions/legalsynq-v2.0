using Identity.Application.DTOs;
using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IProductRoleResolutionService
{
    Task<EffectiveAccessContext> ResolveAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Overload that accepts pre-loaded scoped assignments from the call site,
    /// eliminating the redundant DB round-trip when the caller already has them.
    /// </summary>
    Task<EffectiveAccessContext> ResolveAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyList<ScopedRoleAssignment> preloadedScopedAssignments,
        CancellationToken ct = default);
}
