namespace Intake.Domain.Classification;

public sealed class ArtifactClassification
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntakeArtifactId { get; set; }
    public string ArtifactSha256 { get; set; } = string.Empty;
    public string ClassificationProfileCode { get; set; } = string.Empty;
    public int ClassificationProfileVersion { get; set; }
    public string TaxonomyCode { get; set; } = string.Empty;
    public int TaxonomyVersion { get; set; }
    public string PromptCode { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public int OutputSchemaVersion { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string ExecutionKey { get; set; } = string.Empty;
    public string? ProviderResponseId { get; set; }
    public string Status { get; set; } = ClassificationStatuses.Pending;
    public string? DecisionStatus { get; set; }
    public string? ClassificationCode { get; set; }
    public string? ClassificationLabel { get; set; }
    public double? Confidence { get; set; }
    public string? Reason { get; set; }
    public string? SafeEvidenceJson { get; set; }
    public int? InputCharacters { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public long? LatencyMs { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public bool IsRetryable { get; set; }
    public bool IsCurrent { get; set; }
    public string? CurrentResultMarker { get; set; }
    public int AttemptCount { get; set; }
    public int AttemptNumber { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}