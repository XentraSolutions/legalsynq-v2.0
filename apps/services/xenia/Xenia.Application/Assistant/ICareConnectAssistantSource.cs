namespace Xenia.Application.Assistant;

public interface ICareConnectAssistantSource
{
    Task<CareConnectReferralLookupOutcome> LookupReferralAsync(
        Guid referralId,
        CancellationToken ct = default);

    Task<CareConnectReferralHistoryLookupOutcome> LookupReferralHistoryAsync(
        Guid referralId,
        int top,
        CancellationToken ct = default);

    Task<CareConnectReferralSearchOutcome> SearchReferralsAsync(
        CareConnectReferralSearchRequest request,
        CancellationToken ct = default);

    Task<CareConnectProviderSearchOutcome> SearchProvidersAsync(
        CareConnectProviderSearchRequest request,
        CancellationToken ct = default);

    Task<CareConnectReferrerSearchOutcome> SearchReferrersAsync(
        CareConnectReferrerSearchRequest request,
        CancellationToken ct = default);

    Task<CareConnectReferralQueueSummaryOutcome> GetReferralQueueSummaryAsync(
        CareConnectReferralQueueSummaryRequest request,
        CancellationToken ct = default);
}

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

public sealed record CareConnectReferralSearchRequest(
    string? SearchText,
    string? ClientName,
    string? CaseNumber,
    string? ProviderName,
    string? ReferrerName,
    string? Status,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int Top);

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

public sealed record CareConnectProviderSearchRequest(
    string? Name,
    string? City,
    string? State,
    bool? AcceptingReferrals,
    int Top);

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

public sealed record CareConnectReferrerSearchRequest(
    string? SearchText,
    string? ReferrerName,
    string? Status,
    int Top);

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

public sealed record CareConnectReferralQueueSummaryRequest(
    string? SearchText,
    string? ProviderName,
    string? ReferrerName,
    int RecentTop);

public sealed record CareConnectReferralQueueSummaryOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalVisibleReferrals,
    IReadOnlyList<CareConnectReferralQueueStatusCount> StatusCounts,
    IReadOnlyList<CareConnectReferralSearchResult> RecentReferrals);

public sealed record CareConnectReferralQueueStatusCount(
    string Status,
    int Count);
