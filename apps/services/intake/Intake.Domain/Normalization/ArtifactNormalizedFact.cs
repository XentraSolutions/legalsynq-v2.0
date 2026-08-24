namespace Intake.Domain.Normalization;

public sealed class ArtifactNormalizedFact
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactNormalizationId { get; set; }
    public Guid ArtifactExtractedFactId { get; set; }
    public string FactCode { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
    public string? NormalizedValue { get; set; }
    public string? NormalizedJson { get; set; }
    public string? ComparisonKey { get; set; }
    public string NormalizationStatus { get; set; } = NormalizationStatuses.NotNormalized;
    public string ValidationStatus { get; set; } = ValidationStatuses.Unverified;
    public string NormalizationMethod { get; set; } = string.Empty;
    public string NormalizationVersion { get; set; } = "1";
    public double SourceConfidence { get; set; }
    public string WarningCodesJson { get; set; } = "[]";
    public string EvidenceReferenceJson { get; set; } = "[]";
    public int Ordinal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}