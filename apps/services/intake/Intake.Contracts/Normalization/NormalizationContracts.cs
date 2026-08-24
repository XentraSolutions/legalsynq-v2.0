namespace Intake.Contracts.Normalization;

public sealed record NormalizationProfileResponse(
    string Code,
    string DisplayName,
    string? Description,
    int Version,
    bool IsActive,
    bool IsSystemDefined,
    string NormalizerVersion,
    string UnicodeForm,
    string ComparisonKeyStrategy,
    string DateCulture,
    string DefaultCountryCode,
    string DefaultCurrencyCode);

public sealed record NormalizedFactResponse(
    Guid Id,
    Guid SourceExtractedFactId,
    string FactCode,
    string DataType,
    string RawValue,
    string? NormalizedValue,
    string? NormalizedJson,
    string? ComparisonKey,
    string NormalizationStatus,
    string ValidationStatus,
    IReadOnlyList<string> WarningCodes,
    double SourceConfidence,
    IReadOnlyList<string> Evidence,
    int Ordinal);

public sealed record ArtifactNormalizationResponse(
    Guid Id,
    Guid ArtifactId,
    Guid ArtifactExtractionId,
    string NormalizationProfileCode,
    int NormalizationProfileVersion,
    string NormalizationVersion,
    string Status,
    bool IsCurrent,
    string? FailureCode,
    string? FailureMessage,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<NormalizedFactResponse> Facts);

public sealed class NormalizeArtifactRequest
{
    public string? ProcessingProfileCode { get; set; }
}