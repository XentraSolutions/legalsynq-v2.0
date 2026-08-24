namespace Intake.Contracts.Extraction;

public sealed record ExtractionProfileResponse(
    string Code,
    string DisplayName,
    string? Description,
    int Version,
    string SchemaCode,
    int SchemaVersion,
    string PromptCode,
    int PromptVersion,
    int OutputSchemaVersion,
    bool IsActive,
    bool IsSystemDefined);

public sealed record ExtractedFactResponse(
    Guid Id,
    string FactCode,
    string DataType,
    string RawValue,
    string? NormalizedCandidateValue,
    double Confidence,
    IReadOnlyList<string> SafeEvidence,
    int FactOrdinal);

public sealed record ArtifactExtractionResponse(
    Guid Id,
    Guid ArtifactId,
    Guid ClassificationId,
    string ClassificationCode,
    string ArtifactSha256,
    string ExtractionProfileCode,
    int ExtractionProfileVersion,
    string SchemaCode,
    int SchemaVersion,
    string PromptCode,
    int PromptVersion,
    string ProviderCode,
    string ModelCode,
    string Status,
    string? FailureCode,
    string? FailureMessage,
    bool IsRetryable,
    bool IsCurrent,
    int? InputCharacters,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    long? LatencyMs,
    int AttemptCount,
    int AttemptNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ExtractedFactResponse> Facts);

public sealed class ExtractArtifactRequest
{
    public string? ProcessingProfileCode { get; set; }
    public bool Retry { get; set; }
}