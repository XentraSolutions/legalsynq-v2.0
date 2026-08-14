namespace Intake.Contracts.Review;

public sealed class IntakeReviewListQuery
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Disposition { get; set; }
    public string? ClassificationCode { get; set; }
    public string? SourceType { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool UnassignedOnly { get; set; }
    public DateTimeOffset? CreatedDateFrom { get; set; }
    public DateTimeOffset? CreatedDateTo { get; set; }
    public int? OlderThanDays { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed record IntakeReviewListResponse(
    IReadOnlyList<IntakeReviewSummaryResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record IntakeReviewSummaryResponse(
    Guid Id,
    Guid ArtifactId,
    Guid ArtifactPolicyEvaluationId,
    string Status,
    string Priority,
    string Disposition,
    string ClassificationCode,
    string SourceType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? AssignedToUserId,
    int Version,
    bool IsStale);

public sealed record IntakeReviewQueueSummaryResponse(
    long Pending,
    long Assigned,
    long InReview,
    long CompletedToday,
    long HighPriority,
    long DuplicateReviews,
    long NoMatchReviews,
    long ConflictedReviews,
    DateTimeOffset? OldestPendingAt);

public sealed record IntakeReviewResponse(
    Guid Id,
    Guid TenantId,
    Guid ArtifactId,
    Guid? ClassificationId,
    Guid? ArtifactExtractionId,
    Guid? ArtifactNormalizationId,
    Guid? ArtifactMatchRunId,
    Guid ArtifactPolicyEvaluationId,
    string Status,
    string Priority,
    string Disposition,
    string ReviewOutcome,
    Guid? AssignedToUserId,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    Guid? CompletedByUserId,
    string? CompletionReasonCode,
    string? CompletionComment,
    int RevisionNumber,
    int Version,
    bool IsStale,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record IntakeReviewWorkspaceResponse(
    IntakeReviewResponse Review,
    IntakeReviewSourceResponse Source,
    IntakeReviewClassificationResponse? Classification,
    IReadOnlyList<IntakeReviewFactResponse> Facts,
    IReadOnlyList<IntakeReviewMatchResponse> Matches,
    IReadOnlyList<IntakeReviewDuplicateResponse> Duplicates,
    IReadOnlyList<IntakeReviewFindingResponse> Findings,
    IReadOnlyList<IntakeReviewCorrectionResponse> Corrections,
    IReadOnlyList<IntakeReviewMatchDecisionResponse> MatchDecisions,
    IReadOnlyList<IntakeReviewDuplicateDecisionResponse> DuplicateDecisions,
    IReadOnlyList<IntakeReviewFindingDecisionResponse> FindingDecisions,
    IReadOnlyList<IntakeReviewActivityResponse> Activities);

public sealed record IntakeReviewSourceResponse(
    string SourceType,
    DateTimeOffset? ReceivedAt,
    string? EmailSubject,
    string? Sender,
    string? ManualTitle,
    string? ManualReference,
    IReadOnlyList<IntakeReviewDocumentResponse> Documents);

public sealed record IntakeReviewDocumentResponse(
    Guid? DocumentId,
    Guid ArtifactId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Reference);

public sealed record IntakeReviewClassificationResponse(
    string? ClassificationCode,
    string? ClassificationLabel,
    decimal Confidence,
    string? Reason,
    bool WasOverridden,
    Guid? CorrectionId,
    bool RequiresReprocessing);

public sealed record IntakeReviewFactResponse(
    string FactCode,
    string DataType,
    string? RawValue,
    string? NormalizedValue,
    string? NormalizedJson,
    double SourceConfidence,
    string NormalizationStatus,
    string ValidationStatus,
    IReadOnlyList<string> WarningCodes,
    IReadOnlyList<string> EvidenceReferences,
    string? EffectiveValue,
    Guid? OriginalExtractedFactId,
    Guid? OriginalNormalizedFactId,
    Guid? CorrectionId,
    string SourceType,
    bool IsHumanCorrected,
    bool IsHumanAdded,
    bool IsRejected);

public sealed record IntakeReviewMatchResponse(
    Guid Id,
    string EntityType,
    Guid CandidateEntityId,
    string DisplayLabel,
    decimal Score,
    int Rank,
    string MatchStatus,
    int MatchedFieldCount,
    int ConflictingFieldCount,
    IReadOnlyList<IntakeReviewMatchFieldResponse> Fields);

public sealed record IntakeReviewMatchFieldResponse(
    string FactCode,
    string Outcome,
    string? ReasonCode);

public sealed record IntakeReviewDuplicateResponse(
    Guid Id,
    string DuplicateType,
    Guid? RelatedArtifactId,
    Guid? RelatedBusinessEntityId,
    string? RelatedBusinessEntityType,
    decimal Score,
    string Status,
    string ReasonCode);

public sealed record IntakeReviewFindingResponse(
    Guid Id,
    string RuleCode,
    string Category,
    string Severity,
    string Outcome,
    string ReasonCode,
    string? EntityType,
    string? FactCode,
    decimal? Score,
    decimal? Threshold,
    IReadOnlyList<string> EvidenceReferences,
    string? CurrentDecision);

public sealed record IntakeReviewCorrectionResponse(
    Guid Id,
    string FactCode,
    string CorrectionType,
    string? CorrectedValue,
    string? NormalizedValue,
    string? ValidationStatus,
    string ReasonCode,
    string? Comment,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool HumanVerified);

public sealed record IntakeReviewMatchDecisionResponse(
    Guid Id,
    string EntityType,
    Guid? ArtifactEntityMatchId,
    Guid? CandidateEntityId,
    string Decision,
    bool IsManualSelection,
    string ReasonCode,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record IntakeReviewDuplicateDecisionResponse(
    Guid Id,
    Guid ArtifactDuplicateSignalId,
    string Decision,
    string ReasonCode,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record IntakeReviewFindingDecisionResponse(
    Guid Id,
    Guid ArtifactPolicyFindingId,
    string Decision,
    string ReasonCode,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record IntakeReviewActivityResponse(
    Guid Id,
    string ActivityType,
    Guid? ActorUserId,
    DateTimeOffset CreatedAt);

public sealed record ReviewedIntakeProjectionResponse(
    Guid ReviewId,
    Guid ArtifactId,
    Guid PolicyEvaluationId,
    IntakeReviewClassificationResponse? ReviewedClassification,
    IReadOnlyList<IntakeReviewFactResponse> ReviewedFacts,
    IReadOnlyList<IntakeReviewMatchDecisionResponse> ReviewedEntityDecisions,
    IReadOnlyList<IntakeReviewDuplicateDecisionResponse> DuplicateDecisions,
    IReadOnlyList<IntakeReviewFindingDecisionResponse> PolicyFindingDecisions,
    string ReviewOutcome);

public sealed class CreateIntakeReviewRequest
{
    public Guid ArtifactId { get; set; }
    public Guid? ArtifactPolicyEvaluationId { get; set; }
}

public sealed class ReviewVersionRequest
{
    public int Version { get; set; }
}

public sealed class AssignIntakeReviewRequest
{
    public Guid? UserId { get; set; }
    public int Version { get; set; }
}

public sealed class AddReviewCorrectionRequest
{
    public string FactCode { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public string CorrectionType { get; set; } = "VALUE_CORRECTION";
    public string? CorrectedValue { get; set; }
    public string? CorrectedJson { get; set; }
    public string DataType { get; set; } = "TEXT";
    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public bool HumanVerified { get; set; }
    public int Version { get; set; }
}

public sealed class ReviewMatchDecisionRequest
{
    public Guid? ArtifactEntityMatchId { get; set; }
    public Guid? CandidateEntityId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public int Version { get; set; }
}

public sealed class ReviewDuplicateDecisionRequest
{
    public string Decision { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public int Version { get; set; }
}

public sealed class ReviewFindingDecisionRequest
{
    public string Decision { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public int Version { get; set; }
}

public sealed class CompleteIntakeReviewRequest
{
    public string Outcome { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? Comment { get; set; }
    public int Version { get; set; }
}