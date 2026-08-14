using System.Text.Json;

namespace Intake.Contracts.Classification;

public sealed record TenantAiPolicyResponse(
    Guid TenantId,
    bool IsEnabled,
    string AccessMode,
    string ProviderCode,
    string ModelCode,
    string? CredentialReference,
    int MaxOutputTokens,
    int TimeoutSeconds,
    int MaxAttempts,
    int PolicyVersion,
    DateTimeOffset UpdatedAt);

public sealed class UpsertTenantAiPolicyRequest
{
    public bool IsEnabled { get; set; }
    public string AccessMode { get; set; } = string.Empty;
    public string ProviderCode { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string? CredentialReference { get; set; }
    public int MaxOutputTokens { get; set; } = 600;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 3;
    public int? PolicyVersion { get; set; }
}

public sealed record ClassificationProfileResponse(
    string Code,
    string DisplayName,
    string? Description,
    int Version,
    string TaxonomyCode,
    int TaxonomyVersion,
    string PromptCode,
    int PromptVersion,
    int OutputSchemaVersion,
    bool IsActive,
    bool IsSystemDefined);

public sealed record ClassificationTaxonomyResponse(
    string Code,
    string DisplayName,
    int Version,
    JsonElement Classes,
    bool IsActive);

public sealed record ArtifactClassificationResponse(
    Guid Id,
    Guid TenantId,
    Guid IntakeArtifactId,
    string ArtifactSha256,
    string ClassificationProfileCode,
    int ClassificationProfileVersion,
    string TaxonomyCode,
    int TaxonomyVersion,
    string PromptCode,
    int PromptVersion,
    int OutputSchemaVersion,
    string ProviderCode,
    string ModelCode,
    string ExecutionKey,
    string? ProviderResponseId,
    string Status,
    string? DecisionStatus,
    string? ClassificationCode,
    string? ClassificationLabel,
    double? Confidence,
    string? Reason,
    IReadOnlyList<string> SafeEvidence,
    int? InputCharacters,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    long? LatencyMs,
    string? FailureCode,
    string? FailureMessage,
    bool IsRetryable,
    bool IsCurrent,
    int AttemptCount,
    int AttemptNumber,
    DateTimeOffset? RequestedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);