namespace Tenant.Application.Interfaces;

public interface IEligibleTenantService
{
    Task<IReadOnlyList<Guid>> ListActiveSynqLiensTenantIdsAsync(CancellationToken ct = default);
}

public interface IEligibleTenantRepository
{
    Task<List<Guid>> ListActiveTenantIdsByProductKeysAsync(
        IReadOnlyCollection<string> productKeys,
        DateTime utcNow,
        CancellationToken ct = default);
}
