namespace Intake.Application.Classification;

public sealed record SynqAiClassificationRequest(
    Guid TenantId,
    string ModelCode,
    string SystemInstructions,
    string DocumentText,
    string FileName,
    string DeclaredContentType,
    string TaxonomyJson,
    string OutputSchemaJson,
    int MaxOutputTokens,
    int OutputSchemaVersion,
    string CorrelationId);

public sealed record SynqAiClassificationResult(
    bool Success,
    string? ClassificationCode,
    string? ClassificationLabel,
    double? Confidence,
    IReadOnlyList<string> SafeEvidence,
    string? ProviderResponseId,
    int? InputTokens,
    int? OutputTokens,
    string? FailureCode,
    string? FailureMessage,
    bool IsRetryable,
    string? Reason = null,
    bool SchemaValid = true);

public interface ISynqAiProvider
{
    string ProviderCode { get; }
    bool IsConfigured { get; }

    Task<SynqAiClassificationResult> ClassifyAsync(
        SynqAiClassificationRequest request,
        string credentialReference,
        CancellationToken cancellationToken);
}

[Flags]
public enum SynqAiProviderCapabilities
{
    None = 0,
    Classification = 1,
    StructuredExtraction = 2,
}

public interface ISynqAiProviderCapabilities
{
    SynqAiProviderCapabilities Capabilities { get; }
}

public sealed record SynqAiExtractionRequest(
    Guid TenantId,
    string ModelCode,
    string SystemInstructions,
    string DocumentText,
    string FileName,
    string DeclaredContentType,
    string ClassificationCode,
    string FactCatalogJson,
    string OutputSchemaJson,
    int MaxOutputTokens,
    int OutputSchemaVersion,
    string CorrelationId);

public sealed record SynqAiExtractedFact(
    string FactCode,
    string DataType,
    string RawValue,
    string? NormalizedCandidateValue,
    double Confidence,
    IReadOnlyList<string> SafeEvidence,
    int FactOrdinal);

public sealed record SynqAiExtractionResult(
    bool Success,
    IReadOnlyList<SynqAiExtractedFact> Facts,
    string? ProviderResponseId,
    int? InputTokens,
    int? OutputTokens,
    string? FailureCode,
    string? FailureMessage,
    bool IsRetryable,
    bool SchemaValid = true);

public interface ISynqAiStructuredExtractionProvider
{
    Task<SynqAiExtractionResult> ExtractAsync(
        SynqAiExtractionRequest request,
        string credentialReference,
        CancellationToken cancellationToken);
}

public interface ISynqAiProviderRegistry
{
    IReadOnlyList<string> AvailableProviderCodes { get; }
    ISynqAiProvider GetRequired(string providerCode);
}

public interface IAiCredentialResolver
{
    Task<string?> ResolveAsync(
        Guid tenantId,
        string credentialReference,
        CancellationToken cancellationToken);
}

public interface IIntakeArtifactContentReader
{
    Task<ArtifactContentReadResult> ReadAsync(
        Guid tenantId,
        Intake.Domain.Artifacts.IntakeArtifact artifact,
        int maxCharacters,
        CancellationToken cancellationToken);
}

public sealed record ArtifactContentReadResult(
    bool Success,
    string? Text,
    int CharacterCount,
    string? FailureCode,
    string? FailureMessage,
    bool IsRetryable,
    string? ObservedSha256 = null);

public interface IIntakeDocumentContentClient
{
    Task<Stream?> DownloadAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken);
}