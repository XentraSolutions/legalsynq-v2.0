namespace Intake.Contracts.Policy;

public sealed record PolicyProfileResponse(
    string Code,
    string DisplayName,
    string? Description,
    int Version,
    bool IsActive,
    bool IsSystemDefined);

public sealed record PolicyFindingResponse(
    Guid Id,
    string RuleCode,
    string RuleCategory,
    string Severity,
    string Outcome,
    string ReasonCode,
    string? EntityType,
    string? FactCode,
    Guid? RelatedEntityMatchId,
    Guid? RelatedDuplicateSignalId,
    Guid? RelatedNormalizedFactId,
    decimal? Score,
    decimal? Threshold,
    IReadOnlyList<string> EvidenceReferences);

public sealed record ArtifactPolicyResponse(
    Guid Id,
    Guid ArtifactId,
    Guid? ClassificationId,
    Guid? ArtifactExtractionId,
    Guid? ArtifactNormalizationId,
    Guid? ArtifactMatchRunId,
    string PolicyProfileCode,
    int PolicyProfileVersion,
    string Status,
    string Disposition,
    decimal OverallConfidence,
    string ReviewPriority,
    bool IsCurrent,
    string? FailureCode,
    string? FailureMessage,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<PolicyFindingResponse> Findings);

public sealed class EvaluateArtifactPolicyRequest
{
    public string? ProcessingProfileCode { get; set; }
    public bool Retry { get; set; }
}