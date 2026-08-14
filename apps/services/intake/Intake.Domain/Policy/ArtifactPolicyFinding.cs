namespace Intake.Domain.Policy;

public sealed class ArtifactPolicyFinding
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactPolicyEvaluationId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleCategory { get; set; } = string.Empty;
    public string Severity { get; set; } = PolicyFindingSeverities.Info;
    public string Outcome { get; set; } = PolicyFindingOutcomes.NotApplicable;
    public string ReasonCode { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? FactCode { get; set; }
    public Guid? RelatedEntityMatchId { get; set; }
    public Guid? RelatedDuplicateSignalId { get; set; }
    public Guid? RelatedNormalizedFactId { get; set; }
    public decimal? Score { get; set; }
    public decimal? Threshold { get; set; }
    public string EvidenceReferenceJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
}