using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Integration.Services;

/// <summary>
/// Deterministic recommendation engine. Pure read-model: never writes,
/// never enforces. The mapping table is documented in
/// <c>analysis/COM-B08-host-integration-contract.md</c>.
/// </summary>
internal sealed class CommerceAccessRecommendationService : ICommerceAccessRecommendationService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IHostTenantResolver _tenantResolver;

    public CommerceAccessRecommendationService(
        CommerceDbContext db,
        IClock clock,
        IHostTenantResolver tenantResolver)
    {
        _db = db;
        _clock = clock;
        _tenantResolver = tenantResolver;
    }

    public async Task<AccessRecommendationResponse?> GetForBillingAccountAsync(
        Guid billingAccountId,
        CancellationToken ct)
    {
        var account = await _db.BillingAccounts
            .Where(a => a.Id == billingAccountId)
            .Select(a => new { a.Id, a.Status })
            .FirstOrDefaultAsync(ct);
        if (account is null) return null;

        var standing = await _db.AccountStandings
            .Where(s => s.BillingAccountId == billingAccountId)
            .Select(s => new { s.Status, s.Reason })
            .FirstOrDefaultAsync(ct);

        var hasActiveOrTrialing = await _db.Subscriptions
            .AnyAsync(s => s.BillingAccountId == billingAccountId
                           && (s.Status == SubscriptionStatus.Active
                               || s.Status == SubscriptionStatus.Trialing),
                       ct);

        var (recommendation, reason) = ComputeRecommendation(
            billingAccountStatus: account.Status,
            standingStatus: standing?.Status,
            standingReason: standing?.Reason,
            hasActiveOrTrialing: hasActiveOrTrialing);

        var hostRef = await _tenantResolver.ResolveByBillingAccountAsync(billingAccountId, ct);

        return new AccessRecommendationResponse(
            BillingAccountId: billingAccountId,
            HostPlatformKey: hostRef?.HostPlatformKey,
            ExternalTenantId: hostRef?.ExternalTenantId,
            Recommendation: recommendation,
            Reason: reason,
            AccountStandingStatus: (standing?.Status ?? AccountStandingStatus.Good).ToString(),
            HasActiveOrTrialingSubscription: hasActiveOrTrialing,
            GeneratedAtUtc: _clock.UtcNow);
    }

    /// <summary>
    /// Pure mapping function: see contract document for the rationale of
    /// each rule. No side effects.
    /// </summary>
    internal static (AccessRecommendation Recommendation, string Reason) ComputeRecommendation(
        BillingAccountStatus billingAccountStatus,
        AccountStandingStatus? standingStatus,
        string? standingReason,
        bool hasActiveOrTrialing)
    {
        // Billing-account-level overrides take precedence.
        if (billingAccountStatus == BillingAccountStatus.Closed)
            return (AccessRecommendation.Block,
                "Billing account is closed.");
        if (billingAccountStatus == BillingAccountStatus.Suspended)
            return (AccessRecommendation.Block,
                "Billing account is suspended.");

        // No standing record yet → unknown.
        if (standingStatus is null)
            return (AccessRecommendation.Unknown,
                "No account-standing record exists for this billing account.");

        switch (standingStatus.Value)
        {
            case AccountStandingStatus.Closed:
                return (AccessRecommendation.Block, "Account standing: Closed.");
            case AccountStandingStatus.Suspended:
                return (AccessRecommendation.Block, "Account standing: Suspended.");
            case AccountStandingStatus.PastDue:
                // Documented choice: PastDue → ReadOnly. Rationale: the
                // grace window is owned by GracePeriod; PastDue is the
                // post-grace state where writes should stop but data
                // should remain visible for remediation.
                return (AccessRecommendation.ReadOnly,
                    string.IsNullOrWhiteSpace(standingReason)
                        ? "Account standing: PastDue."
                        : $"Account standing: PastDue ({standingReason}).");
            case AccountStandingStatus.GracePeriod:
                return (AccessRecommendation.GraceLimited,
                    string.IsNullOrWhiteSpace(standingReason)
                        ? "Account standing: GracePeriod."
                        : $"Account standing: GracePeriod ({standingReason}).");
            case AccountStandingStatus.Trialing:
                return (AccessRecommendation.Allow, "Account standing: Trialing.");
            case AccountStandingStatus.Good:
                if (!hasActiveOrTrialing)
                {
                    // Documented choice: Good standing without an
                    // active/trialing subscription → ReadOnly. The
                    // account is not commercially blocked, but has no
                    // current entitlement.
                    return (AccessRecommendation.ReadOnly,
                        "No active or trialing subscription.");
                }
                return (AccessRecommendation.Allow, "Account standing: Good.");
            case AccountStandingStatus.Cancelled:
                // Documented choice: Cancelled → ReadOnly (mirrors the
                // "no entitlement, but not blocked" stance for Good
                // without subscription).
                return (AccessRecommendation.ReadOnly,
                    "Account standing: Cancelled.");
            default:
                return (AccessRecommendation.Unknown,
                    $"Unhandled account standing status: {standingStatus}.");
        }
    }
}
