namespace CareConnect.Application.DTOs;

public sealed record CareConnectReferralLookupOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    CareConnectReferralLookupResult? Referral);

public sealed record CareConnectReferralLookupResult(
    Guid ReferralId,
    string Status,
    string Urgency,
    string ProviderName,
    string ClientDisplayName,
    string? RequestedService,
    string? TreatmentTypeName,
    string? ReferringOrganizationName,
    string? ReferrerName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<CareConnectReferralHistoryLookupItem> History);

public sealed record CareConnectReferralHistoryLookupItem(
    string OldStatus,
    string NewStatus,
    DateTime ChangedAtUtc,
    string? Notes);

public sealed record CareConnectReferralHistoryLookupOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    CareConnectReferralHistoryLookupResult? ReferralHistory);

public sealed record CareConnectReferralHistoryLookupResult(
    Guid ReferralId,
    string ClientDisplayName,
    string ProviderName,
    string CurrentStatus,
    IReadOnlyList<CareConnectReferralHistoryLookupItem> History);

public sealed record CareConnectReferralSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    IReadOnlyList<CareConnectReferralSearchResult> Referrals);

public sealed record CareConnectReferralSearchResult(
    Guid ReferralId,
    string ClientDisplayName,
    string Status,
    string Urgency,
    string ProviderName,
    string? RequestedService,
    string? TreatmentTypeName,
    string? ReferringOrganizationName,
    string? ReferrerName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CareConnectProviderSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    IReadOnlyList<CareConnectProviderSearchResult> Providers);

public sealed record CareConnectProviderSearchResult(
    Guid ProviderId,
    string Name,
    string? OrganizationName,
    string City,
    string State,
    bool AcceptingReferrals,
    bool IsActive,
    string? PrimaryCategory,
    string DisplayLabel);

public sealed record CareConnectReferrerSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    IReadOnlyList<CareConnectReferrerSearchResult> Referrers);

public sealed record CareConnectReferrerSearchResult(
    string ReferrerName,
    string? ReferrerEmail,
    int ReferralCount,
    int OpenReferralCount,
    DateTime? LastReferralAtUtc);

public sealed record CareConnectReferralQueueSummaryOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalVisibleReferrals,
    int WindowReferralCount,
    int MatchingReferralCount,
    int NewReferralCount,
    int OpenReferralCount,
    int ClosedReferralCount,
    DateTime? WindowFromUtc,
    DateTime? WindowToUtc,
    string? AppliedStatus,
    string? AppliedStatusGroup,
    IReadOnlyList<CareConnectReferralQueueStatusCount> StatusCounts,
    IReadOnlyList<CareConnectReferralSearchResult> RecentReferrals);

public sealed record CareConnectReferralQueueStatusCount(
    string Status,
    int Count);
