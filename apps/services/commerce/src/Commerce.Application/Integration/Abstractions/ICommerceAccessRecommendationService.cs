using Commerce.Contracts.Integration;

namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// Computes Commerce's commercial access recommendation for a given
/// billing account. Pure derivation from <c>AccountStanding</c> +
/// active/trialing subscriptions. No enforcement; no host calls.
/// </summary>
public interface ICommerceAccessRecommendationService
{
    /// <summary>
    /// Compute the recommendation for a billing account. Returns
    /// <c>null</c> when the billing account does not exist.
    /// </summary>
    Task<AccessRecommendationResponse?> GetForBillingAccountAsync(
        Guid billingAccountId,
        CancellationToken ct);
}
