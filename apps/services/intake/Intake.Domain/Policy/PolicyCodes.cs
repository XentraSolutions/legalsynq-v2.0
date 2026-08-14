namespace Intake.Domain.Policy;

public static class PolicyEvaluationStatuses
{
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}

public static class PolicyDispositionCodes
{
    public const string AutoAcceptable = "AUTO_ACCEPTABLE";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string NoMatch = "NO_MATCH";
    public const string Conflicted = "CONFLICTED";
    public const string Duplicate = "DUPLICATE";
    public const string Blocked = "BLOCKED";
    public const string InsufficientData = "INSUFFICIENT_DATA";
}

public static class PolicyReviewPriorities
{
    public const string Low = "LOW";
    public const string Normal = "NORMAL";
    public const string High = "HIGH";
    public const string Urgent = "URGENT";
}

public static class PolicyFindingCategories
{
    public const string Eligibility = "ELIGIBILITY";
    public const string Classification = "CLASSIFICATION";
    public const string Extraction = "EXTRACTION";
    public const string Normalization = "NORMALIZATION";
    public const string Matching = "MATCHING";
    public const string Duplicate = "DUPLICATE";
    public const string Conflict = "CONFLICT";
    public const string Evidence = "EVIDENCE";
    public const string Confidence = "CONFIDENCE";
}

public static class PolicyFindingSeverities
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Review = "REVIEW";
    public const string Blocking = "BLOCKING";
}

public static class PolicyFindingOutcomes
{
    public const string Passed = "PASSED";
    public const string Failed = "FAILED";
    public const string Triggered = "TRIGGERED";
    public const string NotApplicable = "NOT_APPLICABLE";
    public const string InsufficientData = "INSUFFICIENT_DATA";
}

public static class PolicyFailureCodes
{
    public const string ContextIncomplete = "POLICY_CONTEXT_INCOMPLETE";
    public const string ProfileUnavailable = "POLICY_PROFILE_UNAVAILABLE";
    public const string ConfigurationInvalid = "POLICY_CONFIGURATION_INVALID";
    public const string UpstreamMismatch = "POLICY_UPSTREAM_MISMATCH";
    public const string TenantContextInvalid = "POLICY_TENANT_CONTEXT_INVALID";
    public const string EvaluationFailed = "POLICY_EVALUATION_FAILED";
    public const string ExecutionCancelled = "POLICY_EXECUTION_CANCELLED";
}

public static class PolicyReasonCodes
{
    public const string ClassificationUnsupported = "POLICY_CLASSIFICATION_UNSUPPORTED";
    public const string ClassificationFailed = "POLICY_CLASSIFICATION_FAILED";
    public const string ClassificationLowConfidence = "POLICY_CLASSIFICATION_LOW_CONFIDENCE";
    public const string RequiredFactMissing = "POLICY_REQUIRED_FACT_MISSING";
    public const string RequiredFactInvalid = "POLICY_REQUIRED_FACT_INVALID";
    public const string RequiredFactLowConfidence = "POLICY_REQUIRED_FACT_LOW_CONFIDENCE";
    public const string RequiredEntityMissing = "POLICY_REQUIRED_ENTITY_MISSING";
    public const string PatientMatchMissing = "POLICY_PATIENT_MATCH_MISSING";
    public const string PatientMatchBelowThreshold = "POLICY_PATIENT_MATCH_BELOW_THRESHOLD";
    public const string PatientMatchAmbiguous = "POLICY_PATIENT_MATCH_AMBIGUOUS";
    public const string ProviderFacilityMatchMissing = "POLICY_PROVIDER_FACILITY_MATCH_MISSING";
    public const string ProviderFacilityMatchBelowThreshold = "POLICY_PROVIDER_FACILITY_MATCH_BELOW_THRESHOLD";
    public const string ProviderFacilityMatchAmbiguous = "POLICY_PROVIDER_FACILITY_MATCH_AMBIGUOUS";
    public const string CaseMatchMissing = "POLICY_CASE_MATCH_MISSING";
    public const string CaseMatchBelowThreshold = "POLICY_CASE_MATCH_BELOW_THRESHOLD";
    public const string CaseMatchAmbiguous = "POLICY_CASE_MATCH_AMBIGUOUS";
    public const string HardIdentifierMatch = "POLICY_HARD_IDENTIFIER_MATCH";
    public const string HardIdentifierConflict = "POLICY_HARD_IDENTIFIER_CONFLICT";
    public const string HardConflict = "POLICY_HARD_CONFLICT";
    public const string ExactDuplicate = "POLICY_EXACT_DUPLICATE";
    public const string ContentDuplicate = "POLICY_CONTENT_DUPLICATE";
    public const string BusinessDuplicate = "POLICY_BUSINESS_DUPLICATE";
    public const string PossibleDuplicate = "POLICY_POSSIBLE_DUPLICATE";
    public const string EvidenceMissing = "POLICY_EVIDENCE_MISSING";
    public const string NormalizationAmbiguous = "POLICY_NORMALIZATION_AMBIGUOUS";
    public const string OverallConfidenceBelowThreshold = "POLICY_OVERALL_CONFIDENCE_BELOW_THRESHOLD";
    public const string AutoAcceptableDisabled = "POLICY_AUTO_ACCEPTABLE_DISABLED";
    public const string UpstreamMissing = "POLICY_UPSTREAM_MISSING";
    public const string UpstreamMismatch = "POLICY_UPSTREAM_MISMATCH";
}

public static class PolicyUpstreamStages
{
    public const string Classification = "CLASSIFICATION";
    public const string Extraction = "EXTRACTION";
    public const string Normalization = "NORMALIZATION";
    public const string Matching = "MATCHING";
}

public static class PolicyRuleCodes
{
    public const string ClassificationEligibility = "CLASSIFICATION_ELIGIBILITY";
    public const string ClassificationConfidence = "CLASSIFICATION_CONFIDENCE";
    public const string RequiredFacts = "REQUIRED_FACTS";
    public const string CriticalFactConfidence = "CRITICAL_FACT_CONFIDENCE";
    public const string StructuralValidity = "STRUCTURAL_VALIDITY";
    public const string EvidencePresence = "EVIDENCE_PRESENCE";
    public const string PatientMatch = "PATIENT_MATCH";
    public const string ProviderFacilityMatch = "PROVIDER_FACILITY_MATCH";
    public const string CaseMatch = "CASE_MATCH";
    public const string CandidateAmbiguity = "CANDIDATE_AMBIGUITY";
    public const string HardIdentifier = "HARD_IDENTIFIER";
    public const string HardConflict = "HARD_CONFLICT";
    public const string Duplicate = "DUPLICATE";
    public const string NormalizationWarning = "NORMALIZATION_WARNING";
    public const string OverallConfidence = "OVERALL_CONFIDENCE";
    public const string AutoAcceptable = "AUTO_ACCEPTABLE";
}