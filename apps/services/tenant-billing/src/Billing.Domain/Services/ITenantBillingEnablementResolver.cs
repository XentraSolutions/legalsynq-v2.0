namespace Billing.Domain.Services;

/// <summary>
/// TB-DATA-02 — narrow read seam: "is this tenant currently allowed to
/// operate Tenant Billing?" Composes the profile lifecycle resolver
/// (<see cref="ITenantBillingAccountResolver"/>) with the entitlement
/// snapshot to produce a single decision.
///
/// <para>
/// In TB-DATA-02 this resolver is observable only — no existing controller
/// consults it. Enforcement on customer / invoice / payment APIs lives in
/// a later block.
/// </para>
/// </summary>
public interface ITenantBillingEnablementResolver
{
    Task<bool> IsTenantBillingEnabledAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantBillingAccessDecision> GetTenantBillingAccessAsync(
        Guid tenantId, CancellationToken ct = default);
}

public sealed class TenantBillingEnablementResolver : ITenantBillingEnablementResolver
{
    private readonly ITenantBillingEntitlementService _entitlement;

    public TenantBillingEnablementResolver(ITenantBillingEntitlementService entitlement)
    {
        _entitlement = entitlement;
    }

    public async Task<bool> IsTenantBillingEnabledAsync(Guid tenantId, CancellationToken ct = default)
    {
        var decision = await _entitlement.GetAccessRecommendationAsync(tenantId, ct);
        return decision.IsEnabled;
    }

    public Task<TenantBillingAccessDecision> GetTenantBillingAccessAsync(
        Guid tenantId, CancellationToken ct = default)
        => _entitlement.GetAccessRecommendationAsync(tenantId, ct);
}
