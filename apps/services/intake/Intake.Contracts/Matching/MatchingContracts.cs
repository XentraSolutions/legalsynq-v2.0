namespace Intake.Contracts.Matching;

public sealed record MatchingProfileResponse(
    string Code,
    string DisplayName,
    string? Description,
    int Version,
    string ScoringVersion,
    bool IsActive,
    IReadOnlyList<string> EntityTypes);

public sealed record MatchFieldResponse(
    Guid? SourceNormalizedFactId,
    string FactCode,
    string CandidateFieldName,
    string ComparisonMethod,
    string Outcome,
    decimal FieldScore,
    decimal Weight,
    decimal EffectiveWeight,
    decimal WeightedScore,
    string ReasonCode);

public sealed record EntityMatchResponse(
    Guid Id,
    string EntityType,
    Guid CandidateEntityId,
    string CandidateDisplayLabel,
    decimal Score,
    int Rank,
    string MatchStatus,
    bool IsTopCandidate,
    int MatchedFieldCount,
    int ConflictingFieldCount,
    IReadOnlyList<MatchFieldResponse> FieldBreakdown);

public sealed record DuplicateSignalResponse(
    Guid Id,
    string DuplicateType,
    Guid? RelatedArtifactId,
    string? RelatedBusinessEntityType,
    Guid? RelatedBusinessEntityId,
    decimal Score,
    string Status,
    string ReasonCode);

public sealed record ArtifactMatchResponse(
    Guid Id,
    Guid ArtifactId,
    Guid ArtifactNormalizationId,
    string MatchingProfileCode,
    int MatchingProfileVersion,
    string ScoringVersion,
    string Status,
    bool IsCurrent,
    string? FailureCode,
    string? FailureMessage,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<EntityMatchResponse> EntityMatches,
    IReadOnlyList<DuplicateSignalResponse> DuplicateSignals);

public sealed class MatchArtifactRequest
{
    public string? ProcessingProfileCode { get; set; }
}