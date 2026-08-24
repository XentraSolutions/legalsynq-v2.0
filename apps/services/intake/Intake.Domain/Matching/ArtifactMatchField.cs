namespace Intake.Domain.Matching;

public sealed class ArtifactMatchField
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactEntityMatchId { get; set; }
    public Guid? SourceNormalizedFactId { get; set; }
    public string FactCode { get; set; } = string.Empty;
    public string CandidateFieldName { get; set; } = string.Empty;
    public string ComparisonMethod { get; set; } = string.Empty;
    public decimal FieldScore { get; set; }
    public decimal Weight { get; set; }
    public decimal EffectiveWeight { get; set; }
    public decimal WeightedScore { get; set; }
    public string MatchOutcome { get; set; } = MatchOutcomes.NotApplicable;
    public string ReasonCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}