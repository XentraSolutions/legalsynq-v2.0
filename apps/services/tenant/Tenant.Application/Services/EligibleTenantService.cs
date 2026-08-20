using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public sealed class EligibleTenantService : IEligibleTenantService
{
    internal static readonly string[] SynqLiensProductKeys =
    [
        ProductKeys.Liens,
        "synq_liens",
        "synqliens",
        "synqlien",
    ];

    private readonly IEligibleTenantRepository _tenants;
    private readonly TimeProvider _timeProvider;

    public EligibleTenantService(IEligibleTenantRepository tenants, TimeProvider timeProvider)
    {
        _tenants = tenants;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<Guid>> ListActiveSynqLiensTenantIdsAsync(
        CancellationToken ct = default)
    {
        var tenantIds = await _tenants.ListActiveTenantIdsByProductKeysAsync(
            SynqLiensProductKeys,
            _timeProvider.GetUtcNow().UtcDateTime,
            ct);

        return tenantIds;
    }
}
