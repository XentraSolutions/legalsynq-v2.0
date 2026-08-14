namespace Intake.Domain.Extraction;

public sealed class ArtifactExtractedFact
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactExtractionId { get; set; }
    public string FactCode { get; set; } = string.Empty;
    public string DataType { get; set; } = ExtractionFactDataTypes.Text;
    public string RawValue { get; set; } = string.Empty;
    public string? NormalizedCandidateValue { get; set; }
    public double Confidence { get; set; }
    public string? EvidenceJson { get; set; }
    public int FactOrdinal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}