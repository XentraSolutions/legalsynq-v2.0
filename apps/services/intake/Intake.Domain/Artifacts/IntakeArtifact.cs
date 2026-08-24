namespace Intake.Domain.Artifacts;

public sealed class IntakeArtifact
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? OrgId { get; set; }
    public Guid? InboundEmailId { get; set; }
    public Guid? ManualIntakeSubmissionId { get; set; }
    public Guid? TenantIntakeSourceId { get; set; }
    public string ArtifactSourceType { get; set; } = string.Empty;
    public Guid? SourceAttachmentMetadataId { get; set; }

    public string ArtifactKey { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string ArtifactRole { get; set; } = string.Empty;
    public int ArtifactOrdinal { get; set; }
    public string? SourceContentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string EffectiveFileName { get; set; } = string.Empty;
    public string DeclaredContentType { get; set; } = string.Empty;
    public string? DetectedContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public bool IsInline { get; set; }

    public string ProcessingStatus { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public bool IsRetryable { get; set; }
    public int AttemptCount { get; set; }

    public Guid? DocumentsServiceDocumentId { get; set; }
    public Guid? DocumentsServiceVersionId { get; set; }
    public string? DocumentsServiceReference { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}