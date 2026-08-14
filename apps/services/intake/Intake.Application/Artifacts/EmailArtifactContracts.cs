using Intake.Domain.Artifacts;

namespace Intake.Application.Artifacts;

public sealed record ExtractedEmailPart(
    int SourceOrdinal,
    string ArtifactType,
    string ArtifactRole,
    string OriginalFileName,
    string DeclaredContentType,
    string? SourceContentId,
    bool IsInline,
    byte[] Content);

public sealed record EmailArtifactExtractionResult(
    IReadOnlyList<ExtractedEmailPart> Parts,
    string? FailureCode,
    string? FailureMessage);

public sealed record IntakeArtifactResponse(
    Guid Id,
    Guid? InboundEmailId,
    Guid? ManualIntakeSubmissionId,
    string ArtifactSourceType,
    Guid? SourceAttachmentMetadataId,
    string ArtifactKey,
    string ArtifactType,
    string ArtifactRole,
    int ArtifactOrdinal,
    string? SourceContentId,
    string OriginalFileName,
    string EffectiveFileName,
    string DeclaredContentType,
    string? DetectedContentType,
    long SizeBytes,
    string? Sha256,
    bool IsInline,
    string ProcessingStatus,
    string? FailureCode,
    string? FailureMessage,
    bool IsRetryable,
    int AttemptCount,
    Guid? DocumentsServiceDocumentId,
    Guid? DocumentsServiceVersionId,
    string? DocumentsServiceReference,
    DateTimeOffset? UploadedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset UpdatedAt);

public sealed record EmailArtifactProcessingResponse(
    Guid InboundEmailId,
    string EmailProcessingStatus,
    IReadOnlyList<IntakeArtifactResponse> Artifacts);

public sealed record IntakeArtifactReconciliationResponse(
    Guid InboundEmailId,
    int MetadataAttachmentCount,
    int ArtifactAttachmentCount,
    int MissingMetadataCount,
    int MissingArtifactCount,
    IReadOnlyList<string> Warnings);

public sealed record IntakeArtifactAnalyticsResponse(
    Guid TenantId,
    Guid? InboundEmailId,
    long TotalArtifacts,
    long CompletedArtifacts,
    long FailedArtifacts,
    long SkippedArtifacts,
    long PendingArtifacts,
    long ProcessingArtifacts,
    long TotalBytes,
    long UploadedBytes);

public sealed record DocumentsUploadResult(
    bool Success,
    Guid? DocumentId,
    Guid? VersionId,
    string? Reference,
    string? FailureCode,
    string? FailureMessage,
    bool IsRetryable);

public sealed record DocumentsLookupResult(
    bool ServiceAvailable,
    bool Found,
    Guid? DocumentId,
    Guid? VersionId,
    string? Reference,
    string? FailureCode,
    string? FailureMessage);