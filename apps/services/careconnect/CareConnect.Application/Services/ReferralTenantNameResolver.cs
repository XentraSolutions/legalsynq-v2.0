using CareConnect.Application.Interfaces;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public static class ReferralTenantNameResolver
{
    public const string Fallback = "-";

    public static Task<IReadOnlyDictionary<Guid, string>> ResolveForReferralsAsync(
        IEnumerable<Referral> referrals,
        ITenantServiceClient tenantClient,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(referrals);
        return ResolveAsync(referrals.Select(r => r.TenantId), tenantClient, ct);
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IEnumerable<Guid> tenantIds,
        ITenantServiceClient tenantClient,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        ArgumentNullException.ThrowIfNull(tenantClient);

        var distinctTenantIds = tenantIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctTenantIds.Length == 0)
            return new Dictionary<Guid, string>();

        var lookups = distinctTenantIds.Select(async tenantId => new KeyValuePair<Guid, string>(
            tenantId,
            Normalize(await tenantClient.GetDisplayNameAsync(tenantId, ct))));

        var resolvedNames = await Task.WhenAll(lookups);
        return resolvedNames.ToDictionary(x => x.Key, x => x.Value);
    }

    private static string Normalize(string? displayName)
        => string.IsNullOrWhiteSpace(displayName) ? Fallback : displayName;
}
