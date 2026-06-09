using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Integration.Services;

/// <summary>
/// Pure read-model entitlement snapshot. Joins billing account,
/// account standing, subscriptions/items, plans, plan-features and
/// products into a single host-neutral DTO. Never writes; never calls
/// host services.
/// </summary>
internal sealed class CommerceEntitlementSnapshotService : ICommerceEntitlementSnapshotService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IHostTenantResolver _tenantResolver;
    private readonly ICommerceAccessRecommendationService _recommendation;

    public CommerceEntitlementSnapshotService(
        CommerceDbContext db,
        IClock clock,
        IHostTenantResolver tenantResolver,
        ICommerceAccessRecommendationService recommendation)
    {
        _db = db;
        _clock = clock;
        _tenantResolver = tenantResolver;
        _recommendation = recommendation;
    }

    public async Task<CommerceEntitlementSnapshot?> GetByBillingAccountAsync(
        Guid billingAccountId,
        bool includeAllSubscriptionStatuses,
        CancellationToken ct)
    {
        var account = await _db.BillingAccounts
            .Where(a => a.Id == billingAccountId)
            .Select(a => new
            {
                a.Id,
                a.AccountNumber,
                a.DisplayName,
            })
            .FirstOrDefaultAsync(ct);
        if (account is null) return null;

        return await BuildAsync(
            billingAccountId: account.Id,
            accountNumber: account.AccountNumber,
            displayName: account.DisplayName,
            includeAllSubscriptionStatuses: includeAllSubscriptionStatuses,
            ct: ct);
    }

    public async Task<CommerceEntitlementSnapshot?> GetByHostTenantAsync(
        string hostPlatformKey,
        string externalTenantId,
        bool includeAllSubscriptionStatuses,
        CancellationToken ct)
    {
        var billingAccountId = await _tenantResolver.ResolveBillingAccountIdAsync(
            hostPlatformKey, externalTenantId, ct);
        if (billingAccountId is null) return null;
        return await GetByBillingAccountAsync(billingAccountId.Value, includeAllSubscriptionStatuses, ct);
    }

    private async Task<CommerceEntitlementSnapshot> BuildAsync(
        Guid billingAccountId,
        string accountNumber,
        string displayName,
        bool includeAllSubscriptionStatuses,
        CancellationToken ct)
    {
        var standing = await _db.AccountStandings
            .Where(s => s.BillingAccountId == billingAccountId)
            .Select(s => new
            {
                s.Status,
                s.Reason,
                s.GracePeriodEndsAtUtc,
            })
            .FirstOrDefaultAsync(ct);

        var hostRef = await _tenantResolver.ResolveByBillingAccountAsync(billingAccountId, ct);
        var recommendation = await _recommendation.GetForBillingAccountAsync(billingAccountId, ct);

        // Subscriptions (filtered or all).
        var subsQuery = _db.Subscriptions
            .Where(s => s.BillingAccountId == billingAccountId);
        if (!includeAllSubscriptionStatuses)
        {
            subsQuery = subsQuery.Where(s =>
                s.Status == SubscriptionStatus.Active
                || s.Status == SubscriptionStatus.Trialing);
        }
        var subs = await subsQuery
            .Select(s => new
            {
                s.Id,
                s.SubscriptionNumber,
                s.Status,
                s.CurrentPeriodStartUtc,
                s.CurrentPeriodEndUtc,
                s.TrialEndUtc,
                s.CancelAtPeriodEnd,
            })
            .ToListAsync(ct);

        var subIds = subs.Select(s => s.Id).ToList();

        // Subscription items joined to plan keys.
        var itemRows = await (
            from item in _db.SubscriptionItems
            join plan in _db.Plans on item.PlanId equals plan.Id
            where subIds.Contains(item.SubscriptionId)
            select new
            {
                item.Id,
                item.SubscriptionId,
                item.PlanId,
                PlanKey = plan.Key,
                PlanName = plan.Name,
                ProductId = plan.ProductId,
                item.Quantity,
            }).ToListAsync(ct);

        var subRefs = subs
            .OrderByDescending(s => s.CurrentPeriodEndUtc)
            .Select(s => new EntitlementSubscriptionRef(
                SubscriptionId: s.Id,
                SubscriptionNumber: s.SubscriptionNumber,
                Status: s.Status.ToString(),
                CurrentPeriodStartUtc: s.CurrentPeriodStartUtc,
                CurrentPeriodEndUtc: s.CurrentPeriodEndUtc,
                TrialEndUtc: s.TrialEndUtc,
                CancelAtPeriodEnd: s.CancelAtPeriodEnd,
                Items: itemRows
                    .Where(i => i.SubscriptionId == s.Id)
                    .Select(i => new EntitlementSubscriptionItemRef(
                        SubscriptionItemId: i.Id,
                        PlanId: i.PlanId,
                        PlanKey: i.PlanKey,
                        Quantity: i.Quantity))
                    .ToList()))
            .ToList();

        // Distinct plans across all included subscription items.
        var planRefs = itemRows
            .GroupBy(i => i.PlanId)
            .Select(g =>
            {
                var first = g.First();
                return new EntitlementPlanRef(
                    PlanId: first.PlanId,
                    PlanKey: first.PlanKey,
                    PlanName: first.PlanName,
                    ProductId: first.ProductId,
                    ProductKey: null);
            })
            .ToList();

        // Resolve product keys for plans that have a product.
        var productIds = planRefs
            .Where(p => p.ProductId.HasValue)
            .Select(p => p.ProductId!.Value)
            .Distinct()
            .ToList();
        var productRows = productIds.Count == 0
            ? new List<(Guid Id, string Key, string Name)>()
            : await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new ValueTuple<Guid, string, string>(p.Id, p.Key, p.Name))
                .ToListAsync(ct);

        var productLookup = productRows.ToDictionary(p => p.Item1, p => (p.Item2, p.Item3));
        planRefs = planRefs
            .Select(p => p.ProductId.HasValue && productLookup.TryGetValue(p.ProductId.Value, out var prod)
                ? p with { ProductKey = prod.Item1 }
                : p)
            .ToList();

        var productRefs = productRows
            .Select(p => new EntitlementProductRef(p.Item1, p.Item2, p.Item3))
            .ToList();

        // Plan-feature limits across the included plans.
        var planIds = planRefs.Select(p => p.PlanId).Distinct().ToList();
        var limits = planIds.Count == 0
            ? new List<EntitlementFeatureLimit>()
            : await (
                from pf in _db.PlanFeatures
                join f in _db.Features on pf.FeatureId equals f.Id
                join pl in _db.Plans on pf.PlanId equals pl.Id
                where planIds.Contains(pf.PlanId)
                select new EntitlementFeatureLimit(
                    pf.PlanId,
                    pl.Key,
                    pf.FeatureId,
                    f.Key,
                    f.Name,
                    pf.IsEnabled,
                    pf.LimitValue,
                    pf.MeteredIncludedUnits))
                .ToListAsync(ct);

        var standingStatusName = (standing?.Status ?? AccountStandingStatus.Good).ToString();

        return new CommerceEntitlementSnapshot(
            BillingAccountId: billingAccountId,
            AccountNumber: accountNumber,
            DisplayName: displayName,
            HostPlatformKey: hostRef?.HostPlatformKey,
            ExternalTenantId: hostRef?.ExternalTenantId,
            AccountStandingStatus: standingStatusName,
            AccountStandingReason: standing?.Reason,
            AccountStandingGracePeriodEndsAtUtc: standing?.GracePeriodEndsAtUtc,
            AccessRecommendation: recommendation?.Recommendation ?? AccessRecommendation.Unknown,
            Products: productRefs,
            Plans: planRefs,
            Subscriptions: subRefs,
            Limits: limits,
            GeneratedAtUtc: _clock.UtcNow);
    }
}
