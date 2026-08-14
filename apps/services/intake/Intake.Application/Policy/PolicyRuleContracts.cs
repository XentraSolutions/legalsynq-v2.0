using Intake.Contracts.Configuration;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;

namespace Intake.Application.Policy;

public sealed record PolicyEvaluationContext(
    Guid TenantId,
    IntakeArtifact Artifact,
    ArtifactClassification? Classification,
    ArtifactExtraction? Extraction,
    ArtifactNormalization? Normalization,
    ArtifactMatchRun? MatchRun);

public sealed record PolicyRuleContext(
    PolicyEvaluationContext Inputs,
    LienIntakeV1Configuration Configuration,
    PolicyProfileDocument Profile);

public sealed record PolicyFindingDraft(
    string RuleCode,
    string RuleCategory,
    string Severity,
    string Outcome,
    string ReasonCode,
    string? EntityType = null,
    string? FactCode = null,
    Guid? RelatedEntityMatchId = null,
    Guid? RelatedDuplicateSignalId = null,
    Guid? RelatedNormalizedFactId = null,
    decimal? Score = null,
    decimal? Threshold = null,
    IReadOnlyList<string>? EvidenceReferences = null);

public sealed class PolicyEvaluationState
{
    private readonly List<PolicyFindingDraft> findings = [];

    public IReadOnlyList<PolicyFindingDraft> Findings => findings;
    public Dictionary<string, decimal> Components { get; } =
        new(StringComparer.Ordinal);

    public void AddFinding(PolicyFindingDraft finding) => findings.Add(finding);

    public void AddComponent(string code, decimal score)
    {
        Components[code] = Math.Clamp(score, 0m, 1m);
    }
}

public interface IPolicyRule
{
    string Code { get; }
    int Order { get; }

    void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state);
}

public interface IPolicyRuleRegistry
{
    IReadOnlyList<IPolicyRule> Rules { get; }
}