using BuildingBlocks.Authorization;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// Resolves capabilities from ProductRoles via RoleCapabilities.
/// Results are cached by sorted product role code set with a 5-minute TTL.
/// In a multi-instance deployment, swap IMemoryCache for IDistributedCache.
/// </summary>
public sealed class CapabilityService : ICapabilityService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IdentityDbContext _db;
    private readonly IMemoryCache _cache;

    public CapabilityService(IdentityDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<bool> HasCapabilityAsync(
        IReadOnlyCollection<string> productRoleCodes,
        string capabilityCode,
        CancellationToken ct = default)
    {
        var caps = await GetCapabilitiesAsync(productRoleCodes, ct);
        return caps.Contains(capabilityCode);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetCapabilitiesAsync(
        IReadOnlyCollection<string> productRoleCodes,
        CancellationToken ct = default)
    {
        if (productRoleCodes.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheKey = BuildCacheKey(productRoleCodes);

        if (_cache.TryGetValue(cacheKey, out IReadOnlySet<string>? cached) && cached is not null)
            return cached;

        // Single JOIN: ProductRoles → RoleCapabilities → Capabilities
        // Indexes: IX_ProductRoles_Code, PK_RoleCapabilities, IX_RoleCapabilities_CapabilityId
        var caps = await _db.RoleCapabilities
            .AsNoTracking()
            .Where(rc => productRoleCodes.Contains(rc.ProductRole.Code)
                      && rc.ProductRole.IsActive
                      && rc.Capability.IsActive)
            .Select(rc => rc.Capability.Code)
            .Distinct()
            .ToListAsync(ct);

        IReadOnlySet<string> result =
            new HashSet<string>(caps, StringComparer.OrdinalIgnoreCase);

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private static string BuildCacheKey(IReadOnlyCollection<string> codes)
        => "caps:" + string.Join("|", codes.Order(StringComparer.OrdinalIgnoreCase));
}
