namespace CareConnect.Application.DTOs;

public class GetReferralsQuery
{
    public string?   Status          { get; set; }
    public Guid?     ProviderId      { get; set; }
    public string?   SearchText      { get; set; }
    public string?   ClientName      { get; set; }
    public string?   CaseNumber      { get; set; }
    public string?   ProviderName    { get; set; }
    public string?   ReferrerName    { get; set; }
    public string?   Urgency         { get; set; }
    public DateTime? CreatedFrom     { get; set; }
    public DateTime? CreatedTo       { get; set; }
    public int       Page            { get; set; } = 1;
    public int       PageSize        { get; set; } = 20;

    // Org-participant scoping: when set, only referrals involving the specified org are returned.
    public Guid? ReferringOrgId { get; set; }
    public Guid? ReceivingOrgId { get; set; }

    // CC-REFERRER-EMAIL: when set, also includes referrals submitted publicly
    // (no ReferringOrganizationId) whose ReferrerEmail matches this address.
    // Allows law firms that activated their portal after submitting public referrals
    // to see those earlier submissions in their referral list.
    public string? ReferrerEmail { get; set; }

    public bool CrossTenantReceiver { get; set; }

    /// <summary>
    /// Mirrors CrossTenantReceiver for the referring side: when set, the search matches
    /// purely on ReferringOrgId/ReferrerEmail (like a provider's receiving-org match) instead
    /// of first gating on TenantId. This keeps a law firm's referral list resilient to any
    /// tenant-ID drift between the Tenant service and Identity (the same org ID is authoritative
    /// in both), the same way the provider's cross-tenant receiver view already is.
    /// </summary>
    public bool CrossTenantReferrer { get; set; }

    /// <summary>
    /// Optional multi-tenant referrer scope. When populated, search spans these tenant IDs
    /// instead of only the caller's active JWT tenant.
    /// </summary>
    public IReadOnlyList<Guid>? TenantIds { get; set; }

    public Guid? ReferralAttributionId { get; set; }

    /// <summary>
    /// Referral Representative Portal scope (see RepresentativeReferralService). When set,
    /// results are restricted to referrals whose ReferralAttributionId is in this set —
    /// applied unconditionally in the repository regardless of which other branch of the
    /// query is used, so it can never be bypassed by any other query parameter. Never set
    /// this from client-supplied input directly; it must come only from a code the caller
    /// verified server-side for this request (see PublicRepresentativeEndpoints).
    /// </summary>
    public IReadOnlyList<Guid>? RestrictedToAttributionIds { get; set; }
}
