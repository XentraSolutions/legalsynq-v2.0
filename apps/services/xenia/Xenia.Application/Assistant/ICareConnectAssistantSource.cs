namespace Xenia.Application.Assistant;

public interface ICareConnectAssistantSource
{
    Task<CareConnectReferralLookupOutcome> LookupReferralAsync(
        Guid referralId,
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
    string? CaseNumber,
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
