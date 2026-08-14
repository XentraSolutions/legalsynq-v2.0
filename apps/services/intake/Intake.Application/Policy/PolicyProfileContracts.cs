using System.Text.Json;
using Intake.Application.Extraction;
using Intake.Domain.Extraction;
using Intake.Domain.Policy;

namespace Intake.Application.Policy;

public sealed class PolicyProfileDocument
{
    public List<string> RequiredUpstreamStages { get; set; } = [];
    public List<string> SupportedClassifications { get; set; } = [];
    public decimal ClassificationConfidenceThreshold { get; set; } = 0.8m;
    public decimal RequiredFactConfidenceThreshold { get; set; } = 0.7m;
    public Dictionary<string, ClassificationPolicyDefinition> ClassificationPolicies { get; set; } = [];
    public Dictionary<string, decimal> MatchThresholds { get; set; } = [];
    public Dictionary<string, decimal> CandidateMargins { get; set; } = [];
    public List<string> EvidenceRequiredFactCodes { get; set; } = [];
    public List<string> HardConflictReasonCodes { get; set; } = [];
    public Dictionary<string, DuplicatePolicyDefinition> DuplicatePolicies { get; set; } = [];
    public Dictionary<string, decimal> ConfidenceWeights { get; set; } = [];
    public Dictionary<string, decimal> ConfidencePenalties { get; set; } = [];
    public string DefaultDisposition { get; set; } = PolicyDispositionCodes.ReviewRequired;
}

public sealed class ClassificationPolicyDefinition
{
    public List<string> RequiredFacts { get; set; } = [];
    public List<PolicyEntityRequirement> RequiredEntities { get; set; } = [];
}

public sealed class PolicyEntityRequirement
{
    public string Code { get; set; } = string.Empty;
    public List<string> AnyOfEntityTypes { get; set; } = [];
    public bool Required { get; set; } = true;
}

public sealed class DuplicatePolicyDefinition
{
    public string Disposition { get; set; } = PolicyDispositionCodes.Duplicate;
    public string Severity { get; set; } = PolicyFindingSeverities.Review;
    public bool Enabled { get; set; } = true;
}

public static class PolicyProfileDefaults
{
    public const string Code = "LIEN_INTAKE_POLICY_V1";
    public const int Version = 1;

    public static string DefinitionJson { get; } = JsonSerializer.Serialize(
        Build(),
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        });

    public static PolicyProfileDocument Parse(string definitionJson)
    {
        var profile = JsonSerializer.Deserialize<PolicyProfileDocument>(
            definitionJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return profile ?? throw new JsonException("Policy definition must be a JSON object.");
    }

    private static PolicyProfileDocument Build()
    {
        var classifications = new[]
        {
            "MEDICAL_BILL",
            "MEDICAL_RECORD",
            "LIEN_DOCUMENT",
            "LETTER_OF_PROTECTION",
            "EXPLANATION_OF_BENEFITS",
            "SETTLEMENT_DOCUMENT",
            "ATTORNEY_DOCUMENT",
            "CORRESPONDENCE",
            "INSURANCE_DOCUMENT",
        };

        var policies = classifications.ToDictionary(
            code => code,
            code => new ClassificationPolicyDefinition
            {
                RequiredFacts = code switch
                {
                    "MEDICAL_BILL" => ["PATIENT_NAME", "PROVIDER_NAME", "DATE_OF_SERVICE_START"],
                    "MEDICAL_RECORD" => ["PATIENT_NAME", "PROVIDER_NAME"],
                    "LIEN_DOCUMENT" => ["PATIENT_NAME", "PROVIDER_NAME", "LIEN_AMOUNT"],
                    "LETTER_OF_PROTECTION" => ["PATIENT_NAME", "PROVIDER_NAME", "ATTORNEY_NAME"],
                    "EXPLANATION_OF_BENEFITS" => ["PATIENT_NAME", "PROVIDER_NAME", "CLAIM_NUMBER"],
                    "SETTLEMENT_DOCUMENT" => ["PATIENT_NAME", "SETTLEMENT_AMOUNT"],
                    "ATTORNEY_DOCUMENT" => ["PATIENT_NAME", "ATTORNEY_NAME"],
                    "CORRESPONDENCE" => ["PATIENT_NAME"],
                    "INSURANCE_DOCUMENT" => ["PATIENT_NAME", "INSURER_NAME", "CLAIM_NUMBER"],
                    _ => [],
                },
                RequiredEntities =
                [
                    new PolicyEntityRequirement
                    {
                        Code = "PATIENT",
                        AnyOfEntityTypes = ["PATIENT"],
                    },
                    new PolicyEntityRequirement
                    {
                        Code = "PROVIDER_OR_FACILITY",
                        AnyOfEntityTypes = ["PROVIDER", "FACILITY"],
                        Required = code is not "SETTLEMENT_DOCUMENT" and not "ATTORNEY_DOCUMENT",
                    },
                ],
            },
            StringComparer.Ordinal);

        return new PolicyProfileDocument
        {
            RequiredUpstreamStages =
            [
                PolicyUpstreamStages.Classification,
                PolicyUpstreamStages.Extraction,
                PolicyUpstreamStages.Normalization,
                PolicyUpstreamStages.Matching,
            ],
            SupportedClassifications = classifications.ToList(),
            ClassificationConfidenceThreshold = 0.80m,
            RequiredFactConfidenceThreshold = 0.70m,
            ClassificationPolicies = policies,
            MatchThresholds = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["PATIENT"] = 0.85m,
                ["PROVIDER"] = 0.80m,
                ["FACILITY"] = 0.80m,
                ["CASE"] = 0.75m,
                ["ATTORNEY"] = 0.80m,
                ["LAW_FIRM"] = 0.80m,
            },
            CandidateMargins = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["PATIENT"] = 0.10m,
                ["PROVIDER"] = 0.10m,
                ["FACILITY"] = 0.10m,
                ["CASE"] = 0.10m,
                ["ATTORNEY"] = 0.10m,
                ["LAW_FIRM"] = 0.10m,
            },
            EvidenceRequiredFactCodes =
            [
                "PATIENT_NAME",
                "PATIENT_IDENTIFIER",
                "PROVIDER_NAME",
                "PROVIDER_IDENTIFIER",
                "LIEN_AMOUNT",
            ],
            HardConflictReasonCodes =
            [
                "DOB_CONFLICT",
                "IDENTIFIER_CONFLICT",
            ],
            DuplicatePolicies = new Dictionary<string, DuplicatePolicyDefinition>(StringComparer.Ordinal)
            {
                ["EXACT_ARTIFACT"] = new(),
                ["CONTENT"] = new(),
                ["BUSINESS_KEY"] = new(),
            },
            ConfidenceWeights = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["classification"] = 0.20m,
                ["extraction"] = 0.15m,
                ["normalization"] = 0.15m,
                ["patient"] = 0.25m,
                ["provider"] = 0.15m,
                ["evidence"] = 0.10m,
            },
            ConfidencePenalties = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["hard_conflict"] = 0.30m,
                ["ambiguity"] = 0.15m,
                ["duplicate"] = 0.25m,
                ["warning"] = 0.05m,
            },
        };
    }

    public static void Validate(PolicyProfileDocument profile)
    {
        if (profile.RequiredUpstreamStages.Count == 0 ||
            profile.RequiredUpstreamStages.Any(stage => stage is not
                (PolicyUpstreamStages.Classification or
                 PolicyUpstreamStages.Extraction or
                 PolicyUpstreamStages.Normalization or
                 PolicyUpstreamStages.Matching)))
            throw new InvalidOperationException("Policy profile contains an invalid upstream stage.");

        if (profile.SupportedClassifications.Count == 0 ||
            profile.SupportedClassifications.Any(code => string.IsNullOrWhiteSpace(code)))
            throw new InvalidOperationException("Policy profile must declare classifications.");

        if (profile.ClassificationConfidenceThreshold is < 0 or > 1 ||
            profile.RequiredFactConfidenceThreshold is < 0 or > 1)
            throw new InvalidOperationException("Policy confidence thresholds must be between 0 and 1.");

        foreach (var policy in profile.ClassificationPolicies.Values)
        {
            if (policy.RequiredFacts.Any(code => !ExtractionFactCatalog.IsKnown(code)))
                throw new InvalidOperationException("Policy profile references an unknown fact code.");
            if (policy.RequiredEntities.Any(entity =>
                    string.IsNullOrWhiteSpace(entity.Code) ||
                    entity.AnyOfEntityTypes.Count == 0))
                throw new InvalidOperationException("Policy profile contains an invalid entity requirement.");
        }

        if (profile.EvidenceRequiredFactCodes.Any(code => !ExtractionFactCatalog.IsKnown(code)))
            throw new InvalidOperationException(
                "Policy profile references an unknown evidence fact code.");
    }
}