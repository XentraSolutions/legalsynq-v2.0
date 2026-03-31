namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-008: Limited referral data exposed to unauthenticated providers via a valid view token.
///
/// Only includes fields that are already present in the provider notification email.
/// No PHI beyond what was sent via email is exposed here.
/// </summary>
public class ReferralPublicSummaryResponse
{
    public Guid   ReferralId       { get; init; }
    public string ClientFirstName  { get; init; } = "";
    public string ClientLastName   { get; init; } = "";
    /// <summary>Referring party name (e.g. law firm contact stored at referral creation time).</summary>
    public string ReferrerName     { get; init; } = "";
    /// <summary>Provider practice or individual name.</summary>
    public string ProviderName     { get; init; } = "";
    public string RequestedService { get; init; } = "";
    public string Status           { get; init; } = "";

    /// <summary>True when the referral is no longer in "New" status (already actioned).</summary>
    public bool IsAlreadyAccepted => Status is not ("New" or "");
}
