using System.Text.Json;
using Intake.Contracts.Configuration;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;

namespace Intake.Application.Policy;

public abstract class PolicyRuleBase(string code, int order) : IPolicyRule
{
    public string Code { get; } = code;
    public int Order { get; } = order;

    public abstract void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state);

    protected static void Finding(
        PolicyEvaluationState state,
        string ruleCode,
        string category,
        string severity,
        string outcome,
        string reasonCode,
        string? entityType = null,
        string? factCode = null,
        Guid? relatedEntityMatchId = null,
        Guid? relatedDuplicateSignalId = null,
        Guid? relatedNormalizedFactId = null,
        decimal? score = null,
        decimal? threshold = null,
        IReadOnlyList<string>? evidence = null) =>
        state.AddFinding(new PolicyFindingDraft(
            ruleCode,
            category,
            severity,
            outcome,
            reasonCode,
            entityType,
            factCode,
            relatedEntityMatchId,
            relatedDuplicateSignalId,
            relatedNormalizedFactId,
            score,
            threshold,
            evidence));

    protected static decimal Threshold(
        PolicyRuleContext context,
        string entityType,
        decimal fallback) =>
        entityType switch
        {
            MatchingEntityTypes.Patient =>
                (decimal)context.Configuration.MinimumPatientMatchScore,
            MatchingEntityTypes.Provider or MatchingEntityTypes.Facility =>
                (decimal)context.Configuration.MinimumProviderFacilityMatchScore,
            MatchingEntityTypes.Case =>
                (decimal)context.Configuration.MinimumCaseMatchScore,
            _ => context.Profile.MatchThresholds.GetValueOrDefault(entityType, fallback),
        };

    protected static decimal Margin(
        PolicyRuleContext context,
        string entityType,
        decimal fallback) =>
        entityType switch
        {
            MatchingEntityTypes.Patient =>
                (decimal)context.Configuration.MinimumPatientMatchMargin,
            MatchingEntityTypes.Provider or MatchingEntityTypes.Facility =>
                (decimal)context.Configuration.MinimumProviderFacilityMatchMargin,
            MatchingEntityTypes.Case =>
                (decimal)context.Configuration.MinimumCaseMatchMargin,
            _ => context.Profile.CandidateMargins.GetValueOrDefault(entityType, fallback),
        };

    protected static bool HasRequiredEntity(
        PolicyRuleContext context,
        ClassificationPolicyDefinition definition,
        string code) =>
        definition.RequiredEntities.Any(requirement =>
            requirement.Required &&
            requirement.Code == code);
}

public sealed class ClassificationEligibilityRule()
    : PolicyRuleBase(PolicyRuleCodes.ClassificationEligibility, 10)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var classification = context.Inputs.Classification;
        if (classification is null)
        {
            if (!context.Profile.RequiredUpstreamStages.Contains(
                    PolicyUpstreamStages.Classification,
                    StringComparer.Ordinal))
                return;
            Finding(
                state,
                Code,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.InsufficientData,
                PolicyReasonCodes.UpstreamMissing);
            return;
        }

        if (!string.Equals(
                classification.Status,
                ClassificationStatuses.Completed,
                StringComparison.Ordinal))
        {
            Finding(
                state,
                Code,
                PolicyFindingCategories.Classification,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.ClassificationFailed,
                factCode: classification.FailureCode);
            return;
        }

        if (string.IsNullOrWhiteSpace(classification.ClassificationCode) ||
            !context.Profile.SupportedClassifications.Contains(
                classification.ClassificationCode,
                StringComparer.Ordinal))
        {
            Finding(
                state,
                Code,
                PolicyFindingCategories.Classification,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.ClassificationUnsupported);
            return;
        }

        state.AddComponent("classification", (decimal)Math.Clamp(
            classification.Confidence ?? 0,
            0,
            1));
    }
}

public sealed class ClassificationConfidenceRule()
    : PolicyRuleBase(PolicyRuleCodes.ClassificationConfidence, 20)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var classification = context.Inputs.Classification;
        if (classification?.Confidence is null)
            return;

        var threshold = Math.Max(
            (decimal)context.Configuration.MinimumClassificationConfidence,
            context.Profile.ClassificationConfidenceThreshold);
        if ((decimal)classification.Confidence.Value < threshold)
        {
            Finding(
                state,
                Code,
                PolicyFindingCategories.Classification,
                PolicyFindingSeverities.Review,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.ClassificationLowConfidence,
                score: (decimal)classification.Confidence.Value,
                threshold: threshold);
        }
    }
}

public sealed class RequiredFactsRule()
    : PolicyRuleBase(PolicyRuleCodes.RequiredFacts, 30)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var classificationCode = context.Inputs.Classification?.ClassificationCode;
        if (classificationCode is null ||
            !context.Profile.ClassificationPolicies.TryGetValue(
                classificationCode,
                out var definition))
            return;

        var facts = context.Inputs.Normalization?.Facts
            .GroupBy(fact => fact.FactCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(fact => fact.SourceConfidence)
                .ThenBy(fact => fact.Ordinal)
                .First(), StringComparer.Ordinal)
            ?? new Dictionary<string, ArtifactNormalizedFact>(StringComparer.Ordinal);

        foreach (var factCode in definition.RequiredFacts.Distinct(StringComparer.Ordinal))
        {
            if (!facts.TryGetValue(factCode, out var fact) ||
                string.IsNullOrWhiteSpace(fact.NormalizedValue) &&
                string.IsNullOrWhiteSpace(fact.ComparisonKey))
            {
                Finding(
                    state,
                    Code,
                    PolicyFindingCategories.Extraction,
                    PolicyFindingSeverities.Blocking,
                    PolicyFindingOutcomes.Triggered,
                    PolicyReasonCodes.RequiredFactMissing,
                    factCode: factCode,
                    relatedNormalizedFactId: fact?.Id);
                continue;
            }

            if (fact.ValidationStatus is ValidationStatuses.InvalidFormat
                or ValidationStatuses.Incomplete
                or ValidationStatuses.Ambiguous)
            {
                Finding(
                    state,
                    Code,
                    PolicyFindingCategories.Normalization,
                    PolicyFindingSeverities.Review,
                    PolicyFindingOutcomes.Triggered,
                    PolicyReasonCodes.RequiredFactInvalid,
                    factCode: factCode,
                    relatedNormalizedFactId: fact.Id);
            }
        }
    }
}

public sealed class CriticalFactConfidenceRule()
    : PolicyRuleBase(PolicyRuleCodes.CriticalFactConfidence, 40)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var classificationCode = context.Inputs.Classification?.ClassificationCode;
        if (classificationCode is null ||
            !context.Profile.ClassificationPolicies.TryGetValue(
                classificationCode,
                out var definition))
            return;

        var required = definition.RequiredFacts
            .Concat(context.Profile.EvidenceRequiredFactCodes)
            .Distinct(StringComparer.Ordinal);
        var threshold = Math.Max(
            (decimal)context.Configuration.MinimumRequiredFactConfidence,
            context.Profile.RequiredFactConfidenceThreshold);
        foreach (var fact in context.Inputs.Normalization?.Facts
                     .Where(fact => required.Contains(fact.FactCode, StringComparer.Ordinal))
                     .GroupBy(fact => fact.FactCode, StringComparer.Ordinal)
                     .Select(group => group
                         .OrderByDescending(fact => fact.SourceConfidence)
                         .ThenBy(fact => fact.Ordinal)
                         .First())
                 ?? [])
        {
            if ((decimal)fact.SourceConfidence < threshold)
            {
                Finding(
                    state,
                    Code,
                    PolicyFindingCategories.Confidence,
                    PolicyFindingSeverities.Review,
                    PolicyFindingOutcomes.Triggered,
                    PolicyReasonCodes.RequiredFactLowConfidence,
                    factCode: fact.FactCode,
                    relatedNormalizedFactId: fact.Id,
                    score: (decimal)fact.SourceConfidence,
                    threshold: threshold);
            }
        }
    }
}

public sealed class StructuralValidityRule()
    : PolicyRuleBase(PolicyRuleCodes.StructuralValidity, 50)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var extraction = context.Inputs.Extraction;
        var normalization = context.Inputs.Normalization;
        var matching = context.Inputs.MatchRun;

        if (context.Profile.RequiredUpstreamStages.Contains(
                PolicyUpstreamStages.Extraction,
                StringComparer.Ordinal) &&
            (extraction is null ||
             extraction.Status != ExtractionStatuses.Completed))
            Finding(
                state,
                Code,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.InsufficientData,
                PolicyReasonCodes.UpstreamMissing);
        else
            state.AddComponent("extraction", 1m);

        if (context.Profile.RequiredUpstreamStages.Contains(
                PolicyUpstreamStages.Normalization,
                StringComparer.Ordinal) &&
            (normalization is null ||
             normalization.Status is not
                 (NormalizationRunStatuses.Completed or NormalizationRunStatuses.Partial)))
            Finding(
                state,
                Code,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.InsufficientData,
                PolicyReasonCodes.UpstreamMissing);
        else if (normalization is not null)
            state.AddComponent(
                "normalization",
                normalization.Status == NormalizationRunStatuses.Completed ? 1m : 0.75m);

        if (context.Profile.RequiredUpstreamStages.Contains(
                PolicyUpstreamStages.Matching,
                StringComparer.Ordinal) &&
            (matching is null ||
             matching.Status is not
                 (MatchRunStatuses.Completed or MatchRunStatuses.Partial)))
            Finding(
                state,
                Code,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.InsufficientData,
                PolicyReasonCodes.UpstreamMissing);
    }
}

public sealed class EvidencePresenceRule()
    : PolicyRuleBase(PolicyRuleCodes.EvidencePresence, 60)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var facts = context.Inputs.Normalization?.Facts ?? [];
        var required = context.Profile.EvidenceRequiredFactCodes;
        if (required.Count == 0)
        {
            state.AddComponent("evidence", 1m);
            return;
        }

        var observed = 0;
        foreach (var factCode in required.Distinct(StringComparer.Ordinal))
        {
            var fact = facts
                .Where(item => item.FactCode == factCode)
                .OrderByDescending(item => item.SourceConfidence)
                .ThenBy(item => item.Ordinal)
                .FirstOrDefault();
            var evidence = ParseArray(fact?.EvidenceReferenceJson);
            if (fact is not null && evidence.Count > 0)
                observed++;
            else if (context.Configuration.RequireEvidenceForCriticalFacts)
                Finding(
                    state,
                    Code,
                    PolicyFindingCategories.Evidence,
                    PolicyFindingSeverities.Review,
                    PolicyFindingOutcomes.Triggered,
                    PolicyReasonCodes.EvidenceMissing,
                    factCode: factCode,
                    relatedNormalizedFactId: fact?.Id);
        }

        state.AddComponent("evidence", (decimal)observed / required.Count);
    }

    private static IReadOnlyList<string> ParseArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public abstract class RequiredEntityMatchRuleBase(
    string code,
    int order,
    string entityCode,
    string reasonMissing,
    string reasonBelowThreshold,
    string reasonAmbiguous)
    : PolicyRuleBase(code, order)
{
    protected string EntityCode { get; } = entityCode;
    protected string ReasonMissing { get; } = reasonMissing;
    protected string ReasonBelowThreshold { get; } = reasonBelowThreshold;
    protected string ReasonAmbiguous { get; } = reasonAmbiguous;

    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var classificationCode = context.Inputs.Classification?.ClassificationCode;
        if (classificationCode is null ||
            !context.Profile.ClassificationPolicies.TryGetValue(
                classificationCode,
                out var definition))
            return;

        var requirement = definition.RequiredEntities.FirstOrDefault(item =>
            item.AnyOfEntityTypes.Contains(EntityCode, StringComparer.Ordinal) ||
            item.AnyOfEntityTypes.Contains(EntityCode == "PROVIDER_OR_FACILITY"
                ? MatchingEntityTypes.Provider
                : EntityCode, StringComparer.Ordinal));
        if (requirement is null)
            return;

        if (EntityCode == "PROVIDER_OR_FACILITY")
        {
            EvaluateProviderOrFacility(context, state, requirement);
            return;
        }

        var entityType = EntityCode;
        var matches = context.Inputs.MatchRun?.EntityMatches
            .Where(match => match.EntityType == entityType)
            .OrderBy(match => match.Rank)
            .ThenByDescending(match => match.Score)
            .ThenBy(match => match.CandidateEntityId)
            .ToArray() ?? [];
        EvaluateBest(context, state, requirement, entityType, matches);
    }

    private void EvaluateProviderOrFacility(
        PolicyRuleContext context,
        PolicyEvaluationState state,
        PolicyEntityRequirement requirement)
    {
        var candidates = context.Inputs.MatchRun?.EntityMatches
            .Where(match => match.EntityType is MatchingEntityTypes.Provider or MatchingEntityTypes.Facility)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.EntityType)
            .ThenBy(match => match.Rank)
            .ThenBy(match => match.CandidateEntityId)
            .ToArray() ?? [];
        EvaluateBest(context, state, requirement, "PROVIDER_OR_FACILITY", candidates);
    }

    private void EvaluateBest(
        PolicyRuleContext context,
        PolicyEvaluationState state,
        PolicyEntityRequirement requirement,
        string entityType,
        IReadOnlyList<Domain.Matching.ArtifactEntityMatch> matches)
    {
        if (matches.Count == 0)
        {
            if (requirement.Required)
                Finding(
                    state,
                    Code,
                    PolicyFindingCategories.Matching,
                    PolicyFindingSeverities.Review,
                    PolicyFindingOutcomes.Triggered,
                    ReasonMissing,
                    entityType: entityType);
            return;
        }

        var best = matches[0];
        var threshold = Threshold(
            context,
            best.EntityType,
            context.Profile.MatchThresholds.GetValueOrDefault(entityType, 0.8m));
        state.AddComponent(
            EntityCode == "PATIENT" ? "patient" :
            EntityCode == "PROVIDER_OR_FACILITY" ? "provider" :
            EntityCode.ToLowerInvariant(),
            best.Score);
        if (best.Score < threshold)
            Finding(
                state,
                Code,
                PolicyFindingCategories.Matching,
                requirement.Required
                    ? PolicyFindingSeverities.Review
                    : PolicyFindingSeverities.Warning,
                PolicyFindingOutcomes.Triggered,
                ReasonBelowThreshold,
                entityType: best.EntityType,
                relatedEntityMatchId: best.Id,
                score: best.Score,
                threshold: threshold);

        var second = matches.Skip(1).FirstOrDefault();
        var margin = Margin(
            context,
            best.EntityType,
            context.Profile.CandidateMargins.GetValueOrDefault(entityType, 0.1m));
        if (second is not null &&
            best.Score >= threshold &&
            best.Score - second.Score < margin)
            Finding(
                state,
                Code,
                PolicyFindingCategories.Matching,
                context.Configuration.ReviewOnAmbiguousFacts
                    ? PolicyFindingSeverities.Review
                    : PolicyFindingSeverities.Warning,
                PolicyFindingOutcomes.Triggered,
                ReasonAmbiguous,
                entityType: best.EntityType,
                relatedEntityMatchId: best.Id,
                score: best.Score,
                threshold: margin,
                evidence: [$"SECOND_CANDIDATE:{second.Id}"]);
    }
}

public sealed class PatientMatchRule()
    : RequiredEntityMatchRuleBase(
        PolicyRuleCodes.PatientMatch,
        70,
        "PATIENT",
        PolicyReasonCodes.PatientMatchMissing,
        PolicyReasonCodes.PatientMatchBelowThreshold,
        PolicyReasonCodes.PatientMatchAmbiguous)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        if (!context.Configuration.EnablePatientMatching &&
            !context.Configuration.RequirePatientMatch)
            return;
        base.Evaluate(context, state);
    }
}

public sealed class ProviderFacilityMatchRule()
    : RequiredEntityMatchRuleBase(
        PolicyRuleCodes.ProviderFacilityMatch,
        80,
        "PROVIDER_OR_FACILITY",
        PolicyReasonCodes.ProviderFacilityMatchMissing,
        PolicyReasonCodes.ProviderFacilityMatchBelowThreshold,
        PolicyReasonCodes.ProviderFacilityMatchAmbiguous)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        if (!context.Configuration.EnableFacilityMatching &&
            !context.Configuration.RequireProviderOrFacilityMatch)
            return;
        base.Evaluate(context, state);
    }
}

public sealed class CaseMatchRule()
    : RequiredEntityMatchRuleBase(
        PolicyRuleCodes.CaseMatch,
        90,
        MatchingEntityTypes.Case,
        PolicyReasonCodes.CaseMatchMissing,
        PolicyReasonCodes.CaseMatchBelowThreshold,
        PolicyReasonCodes.CaseMatchAmbiguous)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        if (!context.Configuration.RequireCaseMatch &&
            !context.Configuration.EnableCaseMatching)
            return;
        base.Evaluate(context, state);
    }
}

public sealed class HardIdentifierRule()
    : PolicyRuleBase(PolicyRuleCodes.HardIdentifier, 100)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var fields = context.Inputs.MatchRun?.EntityMatches
            .SelectMany(match => match.Fields)
            .ToArray() ?? [];
        var hardFields = fields.Where(field =>
                context.Profile.HardConflictReasonCodes.Contains(
                    field.ReasonCode,
                    StringComparer.Ordinal) ||
                (field.FactCode is "PATIENT_IDENTIFIER" or "PROVIDER_IDENTIFIER" &&
                 field.MatchOutcome == MatchOutcomes.Conflict))
            .ToArray();
        if (hardFields.Length > 0)
            Finding(
                state,
                Code,
                PolicyFindingCategories.Conflict,
                context.Configuration.ReviewOnHardConflict
                    ? PolicyFindingSeverities.Review
                    : PolicyFindingSeverities.Warning,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.HardIdentifierConflict);
        else if (fields.Any(field =>
                     field.MatchOutcome is MatchOutcomes.Exact or MatchOutcomes.NormalizedExact &&
                     field.FactCode is "PATIENT_IDENTIFIER" or "PROVIDER_IDENTIFIER"))
            state.AddComponent("identifier", 1m);
    }
}

public sealed class HardConflictRule()
    : PolicyRuleBase(PolicyRuleCodes.HardConflict, 110)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var conflicts = context.Inputs.MatchRun?.EntityMatches
            .Where(match => match.Fields.Any(field =>
                context.Profile.HardConflictReasonCodes.Contains(
                    field.ReasonCode,
                    StringComparer.Ordinal) ||
                (field.FactCode is "PATIENT_IDENTIFIER" or "PROVIDER_IDENTIFIER" &&
                 field.MatchOutcome == MatchOutcomes.Conflict)))
            .ToArray() ?? [];
        foreach (var match in conflicts)
            Finding(
                state,
                Code,
                PolicyFindingCategories.Conflict,
                context.Configuration.ReviewOnHardConflict
                    ? PolicyFindingSeverities.Review
                    : PolicyFindingSeverities.Warning,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.HardConflict,
                entityType: match.EntityType,
                relatedEntityMatchId: match.Id,
                score: match.Score);
    }
}

public sealed class DuplicateRule()
    : PolicyRuleBase(PolicyRuleCodes.Duplicate, 120)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        foreach (var duplicate in context.Inputs.MatchRun?.DuplicateSignals ?? [])
        {
            var exact = duplicate.DuplicateType == DuplicateTypes.ExactArtifactDuplicate ||
                        duplicate.ReasonCode == MatchReasonCodes.ExactArtifactHash;
            var possible = duplicate.Status is DuplicateStatuses.Possible or DuplicateStatuses.Weak;
            var policyKey = exact
                ? "EXACT_ARTIFACT"
                : duplicate.DuplicateType == DuplicateTypes.BusinessKeyDuplicate
                    ? "BUSINESS_KEY"
                    : duplicate.DuplicateType == DuplicateTypes.ContentDuplicate
                        ? "CONTENT"
                        : "POSSIBLE";
            if (!context.Profile.DuplicatePolicies.TryGetValue(
                    policyKey,
                    out var duplicatePolicy) ||
                !duplicatePolicy.Enabled)
                continue;
            var reason = exact
                ? PolicyReasonCodes.ExactDuplicate
                : duplicate.DuplicateType == DuplicateTypes.BusinessKeyDuplicate
                    ? PolicyReasonCodes.BusinessDuplicate
                    : duplicate.DuplicateType == DuplicateTypes.ContentDuplicate
                        ? PolicyReasonCodes.ContentDuplicate
                        : PolicyReasonCodes.PossibleDuplicate;
            var severity = exact && context.Configuration.BlockOnExactDuplicate
                ? PolicyFindingSeverities.Blocking
                : possible && context.Configuration.ReviewOnPossibleDuplicate
                    ? duplicatePolicy.Severity
                    : duplicatePolicy.Severity;
            Finding(
                state,
                Code,
                PolicyFindingCategories.Duplicate,
                severity,
                PolicyFindingOutcomes.Triggered,
                reason,
                relatedDuplicateSignalId: duplicate.Id,
                score: duplicate.Score);
        }

        if (context.Inputs.MatchRun?.DuplicateSignals.Any() == true)
            state.Components["duplicate"] = 0m;
    }
}

public sealed class NormalizationWarningRule()
    : PolicyRuleBase(PolicyRuleCodes.NormalizationWarning, 130)
{
    public override void Evaluate(
        PolicyRuleContext context,
        PolicyEvaluationState state)
    {
        var warnings = context.Inputs.Normalization?.Facts
            .SelectMany(fact => ParseArray(fact.WarningCodesJson)
                .Select(code => (fact, code)))
            .ToArray() ?? [];
        foreach (var (fact, warning) in warnings)
            Finding(
                state,
                Code,
                PolicyFindingCategories.Normalization,
                context.Configuration.ReviewOnAmbiguousFacts &&
                warning.Contains("AMBIGU", StringComparison.OrdinalIgnoreCase)
                    ? PolicyFindingSeverities.Review
                    : PolicyFindingSeverities.Warning,
                PolicyFindingOutcomes.Triggered,
                warning == "AMBIGUOUS" ||
                warning.Contains("AMBIGU", StringComparison.OrdinalIgnoreCase)
                    ? PolicyReasonCodes.NormalizationAmbiguous
                    : warning,
                factCode: fact.FactCode,
                relatedNormalizedFactId: fact.Id);
        if (warnings.Length > 0)
            state.Components["warning"] = 0m;
    }

    private static IReadOnlyList<string> ParseArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}