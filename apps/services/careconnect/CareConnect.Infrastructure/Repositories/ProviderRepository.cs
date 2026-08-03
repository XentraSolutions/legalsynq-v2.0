// BLK-PERF-01: All read-only queries use AsNoTracking() to avoid EF Core change-tracking overhead.
using CareConnect.Application.DTOs;
using CareConnect.Application.Helpers;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CareConnect.Infrastructure.Repositories;

public class ProviderRepository : IProviderRepository
{
    private static readonly HashSet<string> ProviderSearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "at", "by", "find", "last", "latest", "list", "look",
        "lookup", "me", "org", "organization", "organizations", "provider",
        "providers", "recent", "recently", "search", "sent", "show", "the",
        "to", "up", "with",
    };

    private readonly CareConnectDbContext _db;

    public ProviderRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<(List<ProviderSearchRow> Items, int TotalCount)> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        var baseQuery = BuildBaseQuery(tenantId, query);

        if (HasRadiusSearch(query))
        {
            var candidates = await IncludeProviderLookups(baseQuery)
                .ToListAsync(ct);

            var distanceRows = candidates
                .Select(p => new ProviderSearchRow(p, CalculateDistanceMiles(query.Latitude!.Value, query.Longitude!.Value, p.Latitude!.Value, p.Longitude!.Value)))
                .Where(row => row.DistanceMiles.HasValue && row.DistanceMiles.Value <= query.RadiusMiles!.Value)
                .OrderBy(row => row.DistanceMiles!.Value)
                .ThenBy(row => row.Provider.Name)
                .ToList();

            return (
                distanceRows
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList(),
                distanceRows.Count);
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var ids = await baseQuery
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => p.Id)
            .ToListAsync(ct);

        // BLK-PERF-01: AsNoTracking — provider list is read-only; no change tracking needed.
        var items = await IncludeProviderLookups(_db.Providers.AsNoTracking())
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return (items.Select(p => new ProviderSearchRow(p, null)).ToList(), totalCount);
    }

    public async Task<List<ProviderSearchRow>> GetMarkersAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        var baseQuery = BuildBaseQuery(tenantId, query)
            .Where(p => p.Latitude != null && p.Longitude != null);

        if (HasRadiusSearch(query))
        {
            var candidates = await IncludeProviderLookups(baseQuery)
                .ToListAsync(ct);

            return candidates
                .Select(p => new ProviderSearchRow(p, CalculateDistanceMiles(query.Latitude!.Value, query.Longitude!.Value, p.Latitude!.Value, p.Longitude!.Value)))
                .Where(row => row.DistanceMiles.HasValue && row.DistanceMiles.Value <= query.RadiusMiles!.Value)
                .OrderBy(row => row.DistanceMiles!.Value)
                .ThenBy(row => row.Provider.Name)
                .Take(ProviderGeoHelper.MarkerLimit)
                .ToList();
        }

        var ids = await baseQuery
            .OrderBy(p => p.Name)
            .Take(ProviderGeoHelper.MarkerLimit)
            .Select(p => p.Id)
            .ToListAsync(ct);

        // BLK-PERF-01: AsNoTracking — marker data is read-only.
        var items = await IncludeProviderLookups(_db.Providers.AsNoTracking())
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return items.Select(p => new ProviderSearchRow(p, null)).ToList();
    }

    public async Task<Provider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        // BLK-PERF-01: AsNoTracking — read-only detail fetch.
        return await _db.Providers
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProviderSpecialties)
                .ThenInclude(ps => ps.Specialty)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Provider provider, CancellationToken ct = default)
    {
        await _db.Providers.AddAsync(provider, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Provider provider, CancellationToken ct = default)
    {
        _db.Providers.Update(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncCategoriesAsync(Guid providerId, List<Guid> categoryIds, CancellationToken ct = default)
    {
        var existing = await _db.ProviderCategories
            .Where(pc => pc.ProviderId == providerId)
            .ToListAsync(ct);

        _db.ProviderCategories.RemoveRange(existing);

        if (categoryIds.Count > 0)
        {
            var newLinks = categoryIds.Select(cid => new ProviderCategory
            {
                ProviderId = providerId,
                CategoryId = cid
            });
            await _db.ProviderCategories.AddRangeAsync(newLinks, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncSpecialtiesAsync(Guid providerId, List<Guid> specialtyIds, CancellationToken ct = default)
    {
        var existing = await _db.ProviderSpecialties
            .Where(ps => ps.ProviderId == providerId)
            .ToListAsync(ct);

        _db.ProviderSpecialties.RemoveRange(existing);

        var distinct = specialtyIds.Distinct().ToList();
        if (distinct.Count > 0)
        {
            var newLinks = distinct.Select((sid, index) => new ProviderSpecialty
                {
                    ProviderId = providerId,
                    SpecialtyId = sid,
                    IsPrimary = index == 0
                });
            await _db.ProviderSpecialties.AddRangeAsync(newLinks, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<Provider?> GetByIdCrossAsync(Guid id, CancellationToken ct = default)
    {
        // BLK-PERF-01: AsNoTracking — cross-tenant read used for public referral validation; read-only.
        return await _db.Providers
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProviderSpecialties)
                .ThenInclude(ps => ps.Specialty)
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<Provider> BuildBaseQuery(Guid tenantId, GetProvidersQuery query)
    {
        // Providers are a platform-wide marketplace; all active providers from all tenants
        // are discoverable. The tenantId parameter is retained for future analytics/audit use.
        // BLK-PERF-01: AsNoTracking on base query — all search/marker flows are read-only.
        var q = _db.Providers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            foreach (var token in BuildSearchTokens(query.Name))
            {
                var name = token;
                q = q.Where(p =>
                    p.Name.ToLower().Contains(name) ||
                    (p.OrganizationName != null && p.OrganizationName.ToLower().Contains(name)) ||
                    (p.FirstName != null && p.FirstName.ToLower().Contains(name)) ||
                    (p.LastName != null && p.LastName.ToLower().Contains(name)) ||
                    ((p.FirstName ?? string.Empty).ToLower() + " " + (p.LastName ?? string.Empty).ToLower()).Contains(name));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryCode))
            q = q.Where(p => p.ProviderCategories
                .Any(pc => pc.Category != null && pc.Category.Code == query.CategoryCode));

        if (!string.IsNullOrWhiteSpace(query.SpecialtyCode))
        {
            var specialtyCode = Specialty.NormalizeCode(query.SpecialtyCode);
            q = q.Where(p => p.ProviderSpecialties
                .Any(ps => ps.Specialty != null && ps.Specialty.Code == specialtyCode));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
            q = q.Where(p => p.City == query.City);

        if (!string.IsNullOrWhiteSpace(query.State))
            q = q.Where(p => p.State == query.State);

        if (query.AcceptingReferrals.HasValue)
            q = q.Where(p => p.AcceptingReferrals == query.AcceptingReferrals.Value);

        if (query.IsActive.HasValue)
            q = q.Where(p => p.IsActive == query.IsActive.Value);

        // LSCC-01-003: Admin filter — find provider linked to a specific Identity org
        if (query.OrganizationId.HasValue)
            q = q.Where(p => p.OrganizationId == query.OrganizationId.Value);

        if (query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusMiles.HasValue)
        {
            var (minLat, maxLat, minLon, maxLon) = ProviderGeoHelper.BoundingBox(
                query.Latitude.Value, query.Longitude.Value, query.RadiusMiles.Value);

            q = q.Where(p =>
                p.Latitude  != null && p.Longitude != null &&
                p.Latitude  >= minLat && p.Latitude  <= maxLat &&
                p.Longitude >= minLon && p.Longitude <= maxLon);
        }

        if (query.NorthLat.HasValue && query.SouthLat.HasValue &&
            query.EastLng.HasValue  && query.WestLng.HasValue)
        {
            q = q.Where(p =>
                p.Latitude  != null && p.Longitude != null &&
                p.Latitude  >= query.SouthLat.Value && p.Latitude  <= query.NorthLat.Value &&
                p.Longitude >= query.WestLng.Value  && p.Longitude <= query.EastLng.Value);
        }

        return q;
    }

    private static IQueryable<Provider> IncludeProviderLookups(IQueryable<Provider> query) =>
        query
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProviderSpecialties)
                .ThenInclude(ps => ps.Specialty);

    private static bool HasRadiusSearch(GetProvidersQuery query) =>
        query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusMiles.HasValue;

    private static double CalculateDistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMiles = 3958.7613;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var rLat1 = ToRadians(lat1);
        var rLat2 = ToRadians(lat2);

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(rLat1) * Math.Cos(rLat2) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var clamped = Math.Min(1.0, Math.Max(0.0, a));
        return earthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(clamped), Math.Sqrt(1 - clamped));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static IReadOnlyList<string> BuildSearchTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var tokens = Regex.Split(value.Trim().ToLowerInvariant(), "[^a-z0-9]+")
            .Where(token => token.Length > 1 && !ProviderSearchStopWords.Contains(token))
            .Distinct()
            .ToList();

        if (tokens.Count > 1 && tokens.Any(token => token.Length > 2))
        {
            var descriptiveTokens = tokens
                .Where(token => token.Length > 2 || token.Any(char.IsDigit))
                .ToList();

            if (descriptiveTokens.Count > 0)
                tokens = descriptiveTokens;
        }

        return tokens.Count > 0
            ? tokens
            : [value.Trim().ToLowerInvariant()];
    }

    public async Task<List<Provider>> GetUnlinkedAsync(Guid tenantId, CancellationToken ct = default)
    {
        // BLK-PERF-01: AsNoTracking — admin read-only list.
        return await _db.Providers
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.OrganizationId == null)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Provider?> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId, ct);
    }

    /// <inheritdoc />
    public async Task<Provider?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct = default)
    {
        return await _db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdentityUserId == identityUserId, ct);
    }
}
