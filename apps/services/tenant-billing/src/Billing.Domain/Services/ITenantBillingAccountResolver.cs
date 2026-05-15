using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

/// <summary>
/// TB-DATA-01 — narrow read seam for "given a tenant, what Commerce
/// BillingAccountId should we use?". Returns null when the tenant has no
/// Active profile (Draft / Suspended / Closed are intentionally excluded so
/// downstream charge flows automatically stop honouring the mapping while a
/// tenant is paused, without each caller having to re-implement the rule).
///
/// <para>
/// Implementations MUST NOT make a live HTTP call to the Commerce service.
/// The mapping table is the source of truth for resolution; reconciliation
/// with Commerce is a separate concern.
/// </para>
/// </summary>
public interface ITenantBillingAccountResolver
{
    Task<Guid?> GetBillingAccountIdAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class TenantBillingAccountResolver : ITenantBillingAccountResolver
{
    private readonly ITenantBillingProfileRepository _repo;

    public TenantBillingAccountResolver(ITenantBillingProfileRepository repo) => _repo = repo;

    public async Task<Guid?> GetBillingAccountIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return null;
        var active = await _repo.GetActiveByTenantAsync(tenantId, ct);
        return active?.BillingAccountId;
    }
}
