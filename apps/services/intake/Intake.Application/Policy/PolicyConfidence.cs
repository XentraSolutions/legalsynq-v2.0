using Intake.Contracts.Configuration;
using Intake.Domain.Policy;

namespace Intake.Application.Policy;

public static class PolicyConfidenceCalculator
{
    public static decimal Calculate(
        PolicyEvaluationState state,
        PolicyProfileDocument profile)
    {
        var weighted = 0m;
        var totalWeight = 0m;
        foreach (var (component, weight) in profile.ConfidenceWeights)
        {
            if (weight <= 0 || !state.Components.TryGetValue(component, out var score))
                continue;
            weighted += score * weight;
            totalWeight += weight;
        }

        var confidence = totalWeight == 0 ? 0m : weighted / totalWeight;
        confidence -= Penalty(
            state,
            profile,
            "hard_conflict",
            PolicyReasonCodes.HardConflict,
            PolicyReasonCodes.HardIdentifierConflict);
        confidence -= Penalty(
            state,
            profile,
            "ambiguity",
            PolicyReasonCodes.PatientMatchAmbiguous,
            PolicyReasonCodes.ProviderFacilityMatchAmbiguous,
            PolicyReasonCodes.CaseMatchAmbiguous,
            PolicyReasonCodes.NormalizationAmbiguous);
        confidence -= Penalty(
            state,
            profile,
            "duplicate",
            PolicyReasonCodes.ExactDuplicate,
            PolicyReasonCodes.ContentDuplicate,
            PolicyReasonCodes.BusinessDuplicate,
            PolicyReasonCodes.PossibleDuplicate);
        confidence -= Penalty(
            state,
            profile,
            "warning",
            PolicyReasonCodes.RequiredFactLowConfidence,
            PolicyReasonCodes.EvidenceMissing);
        return Math.Clamp(confidence, 0m, 1m);
    }

    private static decimal Penalty(
        PolicyEvaluationState state,
        PolicyProfileDocument profile,
        string penaltyCode,
        params string[] reasonCodes) =>
        state.Findings.Any(finding =>
            reasonCodes.Contains(finding.ReasonCode, StringComparer.Ordinal))
            ? profile.ConfidencePenalties.GetValueOrDefault(penaltyCode)
            : 0m;
}

public static class PolicyDispositionResolver
{
    public static string Resolve(
        PolicyEvaluationState state,
        decimal confidence,
        LienIntakeV1Configuration configuration,
        PolicyProfileDocument profile)
    {
        if (HasReason(state, PolicyReasonCodes.UpstreamMissing) ||
            HasReason(state, PolicyReasonCodes.UpstreamMismatch) ||
            HasReason(state, PolicyReasonCodes.ClassificationUnsupported) ||
            HasReason(state, PolicyReasonCodes.ClassificationFailed) ||
            HasReason(state, PolicyReasonCodes.RequiredFactMissing))
            return PolicyDispositionCodes.Blocked;

        if (HasConfiguredDuplicateDisposition(
                state,
                profile,
                "EXACT_ARTIFACT",
                PolicyReasonCodes.ExactDuplicate))
            return PolicyDispositionCodes.Duplicate;

        if (configuration.ReviewOnHardConflict &&
            HasReason(
                state,
                PolicyReasonCodes.HardConflict,
                PolicyReasonCodes.HardIdentifierConflict))
            return PolicyDispositionCodes.Conflicted;

        if (HasConfiguredDuplicateDisposition(
                state,
                profile,
                "CONTENT",
                PolicyReasonCodes.ContentDuplicate) ||
            HasConfiguredDuplicateDisposition(
                state,
                profile,
                "BUSINESS_KEY",
                PolicyReasonCodes.BusinessDuplicate))
            return PolicyDispositionCodes.Duplicate;

        if (HasReason(
                state,
                PolicyReasonCodes.PatientMatchMissing,
                PolicyReasonCodes.PatientMatchBelowThreshold,
                PolicyReasonCodes.ProviderFacilityMatchMissing,
                PolicyReasonCodes.ProviderFacilityMatchBelowThreshold,
                PolicyReasonCodes.CaseMatchMissing,
                PolicyReasonCodes.CaseMatchBelowThreshold))
            return PolicyDispositionCodes.NoMatch;

        if (state.Findings.Any(finding =>
                finding.Severity is PolicyFindingSeverities.Review
                or PolicyFindingSeverities.Blocking) ||
            confidence < (decimal)configuration.ReviewThreshold)
            return PolicyDispositionCodes.ReviewRequired;

        var autoAcceptableEnabled =
            configuration.EnableAutoAcceptableDisposition &&
            configuration.AllowAutoApproval;
        if (!autoAcceptableEnabled ||
            profile.DefaultDisposition != PolicyDispositionCodes.AutoAcceptable ||
            confidence < (decimal)configuration.AutoApproveThreshold)
            return PolicyDispositionCodes.ReviewRequired;

        return PolicyDispositionCodes.AutoAcceptable;
    }

    private static bool HasReason(
        PolicyEvaluationState state,
        params string[] reasons) =>
        state.Findings.Any(finding =>
            reasons.Contains(finding.ReasonCode, StringComparer.Ordinal));

    private static bool HasConfiguredDuplicateDisposition(
        PolicyEvaluationState state,
        PolicyProfileDocument profile,
        string policyKey,
        string reasonCode) =>
        profile.DuplicatePolicies.TryGetValue(policyKey, out var policy) &&
        policy.Enabled &&
        policy.Disposition == PolicyDispositionCodes.Duplicate &&
        HasReason(state, reasonCode);
}

public static class PolicyReviewPriorityResolver
{
    public static string Resolve(
        PolicyEvaluationState state,
        string disposition) =>
        disposition is PolicyDispositionCodes.Blocked
            or PolicyDispositionCodes.Conflicted
            or PolicyDispositionCodes.Duplicate
            ? PolicyReviewPriorities.Urgent
            : disposition == PolicyDispositionCodes.NoMatch ||
              state.Findings.Any(finding =>
                  finding.ReasonCode is
                      PolicyReasonCodes.ClassificationLowConfidence or
                      PolicyReasonCodes.RequiredFactLowConfidence)
                ? PolicyReviewPriorities.High
                : disposition == PolicyDispositionCodes.AutoAcceptable
                    ? PolicyReviewPriorities.Low
                    : PolicyReviewPriorities.Normal;
}