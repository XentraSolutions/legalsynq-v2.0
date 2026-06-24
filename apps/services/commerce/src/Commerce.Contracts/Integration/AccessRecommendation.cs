namespace Commerce.Contracts.Integration;

/// <summary>
/// Commerce's commercial access recommendation to a host platform.
///
/// Commerce <em>recommends</em>; the host <em>enforces</em>. Commerce
/// never enforces product-level access in COM-B08.
/// </summary>
public enum AccessRecommendation
{
    /// <summary>Commerce has no opinion (e.g. tenant is unknown to Commerce).</summary>
    Unknown = 0,

    /// <summary>Full commercial access is permitted.</summary>
    Allow = 1,

    /// <summary>
    /// Read-only access is recommended — typically when no active or
    /// trialing subscription exists, or when a billing issue should
    /// degrade write access while keeping data viewable.
    /// </summary>
    ReadOnly = 2,

    /// <summary>
    /// Limited "grace" access is recommended — the account is currently
    /// inside a billing grace window (account standing
    /// <c>GracePeriod</c>) and the host should allow continued use
    /// while surfacing a remediation prompt. Note: post-grace
    /// <c>PastDue</c> downgrades to <see cref="ReadOnly"/> instead.
    /// </summary>
    GraceLimited = 3,

    /// <summary>
    /// Access should be blocked — the account is suspended, closed,
    /// or otherwise commercially unavailable.
    /// </summary>
    Block = 4,
}
