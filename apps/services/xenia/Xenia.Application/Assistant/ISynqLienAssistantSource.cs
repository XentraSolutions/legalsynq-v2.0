namespace Xenia.Application.Assistant;

public interface ISynqLienAssistantSource
{
    Task<SynqLienLienLookupOutcome> LookupLienAsync(
        SynqLienLienLookupRequest request,
        CancellationToken ct = default);

    Task<SynqLienLienSearchOutcome> SearchLiensAsync(
        SynqLienLienSearchRequest request,
        CancellationToken ct = default);

    Task<SynqLienQueueSummaryOutcome> GetLienQueueSummaryAsync(
        SynqLienQueueSummaryRequest request,
        CancellationToken ct = default);

    Task<SynqLienCaseLookupOutcome> LookupCaseAsync(
        SynqLienCaseLookupRequest request,
        CancellationToken ct = default);

    Task<SynqLienCaseSearchOutcome> SearchCasesAsync(
        SynqLienCaseSearchRequest request,
        CancellationToken ct = default);
}

public sealed record SynqLienLienLookupRequest(
    Guid? LienId,
    string? LienNumber);

public sealed record SynqLienLienLookupOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    SynqLienLienLookupResult? Lien);

public sealed record SynqLienLienLookupResult(
    Guid LienId,
    string LienNumber,
    string Status,
    string LienType,
    string SubjectDisplayName,
    Guid? CaseId,
    string? CaseNumber,
    string? CaseTitle,
    decimal OriginalAmount,
    decimal? CurrentBalance,
    decimal? OfferPrice,
    decimal? PurchasePrice,
    decimal? PayoffAmount,
    string? Jurisdiction,
    bool IsConfidential,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SynqLienLienSearchRequest(
    string? SearchText,
    string? SubjectName,
    string? CaseNumber,
    string? Status,
    string? StatusGroup,
    string? LienType,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int Top);

public sealed record SynqLienLienSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    IReadOnlyList<SynqLienLienSearchResult> Liens);

public sealed record SynqLienLienSearchResult(
    Guid LienId,
    string LienNumber,
    string Status,
    string LienType,
    string SubjectDisplayName,
    Guid? CaseId,
    string? CaseNumber,
    decimal OriginalAmount,
    decimal? CurrentBalance,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SynqLienQueueSummaryRequest(
    string? SearchText,
    string? SubjectName,
    string? CaseNumber,
    string? Status,
    string? StatusGroup,
    string? LienType,
    int? Days,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int RecentTop);

public sealed record SynqLienQueueSummaryOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalVisibleLiens,
    int WindowLienCount,
    int MatchingLienCount,
    int DraftLienCount,
    int OpenLienCount,
    int ClosedLienCount,
    DateTime? WindowFromUtc,
    DateTime? WindowToUtc,
    string? AppliedStatus,
    string? AppliedStatusGroup,
    IReadOnlyList<SynqLienStatusCount> StatusCounts,
    IReadOnlyList<SynqLienLienSearchResult> RecentLiens);

public sealed record SynqLienStatusCount(
    string Status,
    int Count);

public sealed record SynqLienCaseLookupRequest(
    Guid? CaseId,
    string? CaseNumber,
    int LiensTop);

public sealed record SynqLienCaseLookupOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    SynqLienCaseLookupResult? Case);

public sealed record SynqLienCaseLookupResult(
    Guid CaseId,
    string CaseNumber,
    string ClientDisplayName,
    string Status,
    string? Title,
    string? CaseType,
    string? CurrentMedicalStatus,
    string? LawFirm,
    string? CaseManager,
    decimal? DemandAmount,
    decimal? SettlementAmount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<SynqLienLienSearchResult> Liens);

public sealed record SynqLienCaseSearchRequest(
    string? SearchText,
    string? ClientName,
    string? CaseNumber,
    string? Status,
    int Top);

public sealed record SynqLienCaseSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    IReadOnlyList<SynqLienCaseSearchResult> Cases);

public sealed record SynqLienCaseSearchResult(
    Guid CaseId,
    string CaseNumber,
    string ClientDisplayName,
    string Status,
    string? Title,
    string? CaseType,
    string? CurrentMedicalStatus,
    string? LawFirm,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
