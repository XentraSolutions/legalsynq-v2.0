namespace Intake.Contracts.Snapshot;

public sealed record ApprovedSnapshotClassification(
    string? OriginalClassification,
    string? EffectiveClassification,
    bool WasHumanOverridden);

public sealed record ApprovedSnapshotFact(
    string FactCode,
    string DataType,
    string? Value,
    string? ValueJson,
    string ValidationStatus,
    string SourceType,
    bool HumanCorrected,
    bool HumanAdded,
    Guid? SourceExtractedFactId,
    Guid? SourceNormalizedFactId,
    Guid? CorrectionId,
    double? SourceConfidence,
    IReadOnlyList<string> EvidenceReferences,
    int Ordinal);

public sealed record ApprovedSnapshotEntityDecision(
    string EntityType,
    string Decision,
    Guid? SelectedEntityId,
    Guid? SourceMatchId,
    bool IsManualSelection,
    string ReasonCode);

public sealed record ApprovedSnapshotDuplicateDecision(
    Guid? SignalId,
    string Decision,
    string ReasonCode);

public sealed record ApprovedSnapshotDocument(
    Guid? DocumentId,
    Guid ArtifactId,
    string DocumentRole,
    string FileName,
    string MimeType,
    string? Sha256,
    string? Reference);

public sealed record ApprovedSnapshotReviewMetadata(
    Guid ReviewId,
    string ReviewOutcome,
    Guid ApprovedByUserId,
    DateTimeOffset ApprovedAt);

public sealed record ApprovedSnapshotProvenance(
    Guid ArtifactId,
    Guid? ClassificationId,
    Guid? ArtifactExtractionId,
    Guid? ArtifactNormalizationId,
    Guid? ArtifactMatchRunId,
    Guid PolicyEvaluationId,
    Guid ReviewId);

public sealed record ApprovedIntakeSnapshotV1(
    string SchemaCode,
    int SchemaVersion,
    int SnapshotVersion,
    string ProcessingProfileCode,
    ApprovedSnapshotClassification Classification,
    IReadOnlyList<ApprovedSnapshotFact> Facts,
    IReadOnlyList<ApprovedSnapshotEntityDecision> Entities,
    IReadOnlyList<ApprovedSnapshotDocument> Documents,
    IReadOnlyList<ApprovedSnapshotDuplicateDecision> DuplicateDecisions,
    ApprovedSnapshotReviewMetadata Review,
    ApprovedSnapshotProvenance Provenance);

public sealed record ApprovedSnapshotResponse(
    Guid SnapshotId,
    Guid ArtifactId,
    Guid ReviewId,
    int SnapshotVersion,
    string SchemaCode,
    int SchemaVersion,
    string ProcessingProfileCode,
    string Status,
    string SnapshotHash,
    bool IsCurrent,
    Guid ApprovedByUserId,
    DateTimeOffset ApprovedAt,
    DateTimeOffset CreatedAt,
    ApprovedIntakeSnapshotV1 Payload);

public sealed record ApprovedSnapshotSummaryResponse(
    Guid SnapshotId,
    Guid ArtifactId,
    Guid ReviewId,
    int SnapshotVersion,
    string SchemaCode,
    int SchemaVersion,
    string ProcessingProfileCode,
    string Status,
    string SnapshotHash,
    bool IsCurrent,
    Guid ApprovedByUserId,
    DateTimeOffset ApprovedAt,
    DateTimeOffset CreatedAt);

public sealed record AdapterDescriptorResponse(
    string AdapterCode,
    string AdapterVersion,
    IReadOnlyList<string> SupportedSnapshotSchemas,
    IReadOnlyList<string> SupportedProcessingProfiles,
    bool SupportsDryRun,
    bool SupportsRetry);

public sealed record AdapterExecutionResponse(
    Guid ExecutionId,
    Guid SnapshotId,
    string AdapterCode,
    string AdapterVersion,
    string ExecutionKey,
    string IdempotencyKey,
    string Status,
    int AttemptNumber,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyList<AdapterExternalReferenceResponse> ExternalReferences);

public sealed record AdapterExternalReferenceResponse(
    string ReferenceType,
    string ReferenceId);

public sealed class ExecuteAdapterRequest
{
    public bool DryRun { get; set; }
}