namespace Billing.Domain.Entities;

/// <summary>
/// TB-DATA-02 — allowed values for
/// <see cref="TenantBillingEntitlementSnapshot.AccessRecommendation"/>.
/// Mirrors the wire format of Commerce's
/// <c>Commerce.Contracts.Integration.AccessRecommendation</c> enum so a
/// future publisher can pass values through verbatim.
///
/// <list type="bullet">
///   <item><c>Unknown</c> — no recommendation; treat as "not enabled".</item>
///   <item><c>Allow</c> — full access.</item>
///   <item><c>ReadOnly</c> — read-only access; writes should be blocked.</item>
///   <item><c>GraceLimited</c> — limited access during a grace window;
///         writes should be blocked but reads remain available.</item>
///   <item><c>Block</c> — no access at all.</item>
/// </list>
/// </summary>
public static class TenantBillingAccessRecommendation
{
    public const string Unknown      = "Unknown";
    public const string Allow        = "Allow";
    public const string ReadOnly     = "ReadOnly";
    public const string GraceLimited = "GraceLimited";
    public const string Block        = "Block";

    public static bool IsValid(string? value)
        => value is Unknown or Allow or ReadOnly or GraceLimited or Block;
}
