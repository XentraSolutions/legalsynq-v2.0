using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Commerce.Domain.Billing;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// Default tenant resolver. "No-op" in the sense that it never reaches
/// out to any host platform — it answers purely from the Commerce-owned
/// <c>BillingAccountExternalRef</c> rows seeded in COM-B03.
///
/// A real host adapter (future LegalSynq integration phase) would
/// replace this with one that synchronises mappings from the host
/// Tenant service. COM-B08 must NOT do that.
/// </summary>
internal sealed class NoopHostTenantResolver : IHostTenantResolver
{
    private readonly CommerceDbContext _db;

    public NoopHostTenantResolver(CommerceDbContext db) => _db = db;

    public async Task<Guid?> ResolveBillingAccountIdAsync(
        string hostPlatformKey,
        string externalTenantId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hostPlatformKey)) return null;
        if (string.IsNullOrWhiteSpace(externalTenantId)) return null;

        var key = HostPlatformKey.Normalize(hostPlatformKey);
        var tenantId = externalTenantId.Trim();

        return await _db.BillingAccountExternalRefs
            .Where(r => r.HostPlatformKey == key && r.ExternalTenantId == tenantId)
            .OrderByDescending(r => r.IsPrimary)
            .ThenBy(r => r.CreatedAtUtc)
            .Select(r => (Guid?)r.BillingAccountId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<HostTenantRef?> ResolveByBillingAccountAsync(
        Guid billingAccountId,
        CancellationToken ct)
    {
        if (billingAccountId == Guid.Empty) return null;

        var row = await _db.BillingAccountExternalRefs
            .Where(r => r.BillingAccountId == billingAccountId)
            .OrderByDescending(r => r.IsPrimary)
            .ThenBy(r => r.CreatedAtUtc)
            .Select(r => new
            {
                r.HostPlatformKey,
                r.ExternalTenantId,
                r.ExternalCustomerRef,
            })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new HostTenantRef(
                HostPlatformKey: row.HostPlatformKey,
                ExternalTenantId: row.ExternalTenantId,
                DisplayName: null,
                MetadataJson: null);
    }
}
