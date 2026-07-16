namespace Liens.Application.DTOs;

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
