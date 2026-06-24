namespace BuildingBlocks.Commerce;

/// <summary>
/// Simplified entitlement result returned by <see cref="ICommerceEntitlementClient"/>.
///
/// <para>
/// This type is intentionally self-contained — it does not reference
/// <c>Commerce.Contracts.Integration.CommerceEntitlementSnapshot</c> directly,
/// keeping consuming services decoupled from Commerce's internal DTO contract.
/// </para>
///
/// <para>
/// <see cref="IsAvailable"/> = <c>false</c> means Commerce did not return data
/// (tenant not found, integration disabled, or HTTP error). Consuming services
/// MUST NOT block business operations on Commerce availability; they should
/// fall through to permissive defaults when <see cref="IsAvailable"/> is false.
/// </para>
/// </summary>
public sealed record CommerceEntitlementResult(
    bool                                IsAvailable,
    string                              AccessRecommendation,
    string                              AccountStandingStatus,
    string?                             AccountStandingReason,
    IReadOnlyList<string>               ProductKeys,
    IReadOnlyList<CommerceEntitlementPlan> Plans,
    DateTimeOffset?                     SnapshotGeneratedAtUtc,
    string?                             BillingAccountId,
    string?                             ExternalTenantId,
    bool                                IsError     = false,
    string?                             ErrorMessage = null)
{
    /// <summary>
    /// Returns an <see cref="IsAvailable"/> = <c>false</c> result representing
    /// a known-but-absent tenant (e.g. not yet registered in Commerce).
    /// </summary>
    public static CommerceEntitlementResult Unavailable(string? reason = null) =>
        new(
            IsAvailable:            false,
            AccessRecommendation:   CommerceAccessRecommendationValues.Unknown,
            AccountStandingStatus:  "Unknown",
            AccountStandingReason:  null,
            ProductKeys:            Array.Empty<string>(),
            Plans:                  Array.Empty<CommerceEntitlementPlan>(),
            SnapshotGeneratedAtUtc: null,
            BillingAccountId:       null,
            ExternalTenantId:       null,
            IsError:                false,
            ErrorMessage:           reason);

    /// <summary>
    /// Returns an <see cref="IsError"/> = <c>true</c> result representing a failed
    /// Commerce integration call (HTTP error, timeout, parse failure, etc.).
    /// </summary>
    public static CommerceEntitlementResult Error(string errorMessage) =>
        new(
            IsAvailable:            false,
            AccessRecommendation:   CommerceAccessRecommendationValues.Unknown,
            AccountStandingStatus:  "Unknown",
            AccountStandingReason:  null,
            ProductKeys:            Array.Empty<string>(),
            Plans:                  Array.Empty<CommerceEntitlementPlan>(),
            SnapshotGeneratedAtUtc: null,
            BillingAccountId:       null,
            ExternalTenantId:       null,
            IsError:                true,
            ErrorMessage:           errorMessage);

    /// <summary>
    /// Returns <c>true</c> when the tenant has an active entitlement for the
    /// specified product key. Case-insensitive comparison.
    /// </summary>
    public bool HasProduct(string productKey) =>
        ProductKeys.Any(k => string.Equals(k, productKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns <c>true</c> when Commerce recommends granting the tenant access.
    /// Evaluates to <c>Allow</c> or <c>GraceLimited</c> (grace window active).
    /// When <see cref="IsAvailable"/> is <c>false</c>, returns <c>false</c> —
    /// callers should apply their own permissive/restrictive fallback policy.
    /// </summary>
    public bool IsAccessAllowed =>
        IsAvailable &&
        (AccessRecommendation == CommerceAccessRecommendationValues.Allow ||
         AccessRecommendation == CommerceAccessRecommendationValues.GraceLimited);
}

/// <summary>
/// Simplified plan reference within a <see cref="CommerceEntitlementResult"/>.
/// </summary>
public sealed record CommerceEntitlementPlan(
    string  PlanKey,
    string  PlanName,
    string? ProductKey);

/// <summary>
/// Canonical string values for <see cref="CommerceEntitlementResult.AccessRecommendation"/>.
/// Mirrors the <c>AccessRecommendation</c> enum in <c>Commerce.Contracts.Integration</c>
/// without introducing a direct project dependency.
/// </summary>
public static class CommerceAccessRecommendationValues
{
    /// <summary>Commerce has no opinion (tenant unknown or integration disabled).</summary>
    public const string Unknown      = "Unknown";

    /// <summary>Full commercial access is permitted.</summary>
    public const string Allow        = "Allow";

    /// <summary>Read-only access recommended — no active or trialing subscription.</summary>
    public const string ReadOnly     = "ReadOnly";

    /// <summary>
    /// Grace-period limited access — account is inside a billing grace window.
    /// Callers should allow continued access while surfacing a remediation prompt.
    /// </summary>
    public const string GraceLimited = "GraceLimited";

    /// <summary>Access should be blocked — account suspended, closed, or past-due beyond grace.</summary>
    public const string Block        = "Block";
}
