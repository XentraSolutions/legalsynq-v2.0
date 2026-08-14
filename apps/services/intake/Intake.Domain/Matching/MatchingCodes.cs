namespace Intake.Domain.Matching;

public static class MatchingEntityTypes
{
    public const string Patient = "PATIENT";
    public const string Provider = "PROVIDER";
    public const string Facility = "FACILITY";
    public const string Attorney = "ATTORNEY";
    public const string LawFirm = "LAW_FIRM";
    public const string Case = "CASE";

    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Patient,
            Provider,
            Facility,
            Attorney,
            LawFirm,
            Case,
        };
}

public static class MatchRunStatuses
{
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
}

public static class MatchOutcomes
{
    public const string Exact = "EXACT";
    public const string NormalizedExact = "NORMALIZED_EXACT";
    public const string Fuzzy = "FUZZY";
    public const string Partial = "PARTIAL";
    public const string Conflict = "CONFLICT";
    public const string Missing = "MISSING";
    public const string NotApplicable = "NOT_APPLICABLE";
}

public static class MatchStatuses
{
    public const string Strong = "STRONG";
    public const string Possible = "POSSIBLE";
    public const string Weak = "WEAK";
    public const string Conflicted = "CONFLICTED";
    public const string InsufficientData = "INSUFFICIENT_DATA";
}

public static class DuplicateTypes
{
    public const string ExactArtifactDuplicate = "EXACT_ARTIFACT_DUPLICATE";
    public const string ContentDuplicate = "CONTENT_DUPLICATE";
    public const string BusinessKeyDuplicate = "BUSINESS_KEY_DUPLICATE";
    public const string PossibleDuplicate = "POSSIBLE_DUPLICATE";
}

public static class DuplicateStatuses
{
    public const string ConfirmedSignal = "CONFIRMED_SIGNAL";
    public const string Likely = "LIKELY";
    public const string Possible = "POSSIBLE";
    public const string Weak = "WEAK";
}

public static class MatchingFailureCodes
{
    public const string NormalizationRequired = "MATCH_NORMALIZATION_REQUIRED";
    public const string ProfileUnavailable = "MATCH_PROFILE_UNAVAILABLE";
    public const string EntityProviderUnavailable = "MATCH_ENTITY_PROVIDER_UNAVAILABLE";
    public const string CandidateSearchFailed = "MATCH_CANDIDATE_SEARCH_FAILED";
    public const string ScoringFailed = "MATCH_SCORING_FAILED";
    public const string DuplicateSearchFailed = "MATCH_DUPLICATE_SEARCH_FAILED";
    public const string TenantContextInvalid = "MATCH_TENANT_CONTEXT_INVALID";
    public const string NoUsableFacts = "MATCH_NO_USABLE_FACTS";
    public const string MatchingDisabled = "MATCHING_DISABLED";
    public const string ExecutionCancelled = "MATCH_EXECUTION_CANCELLED";
}

public static class MatchReasonCodes
{
    public const string NameExact = "NAME_EXACT";
    public const string NameFuzzy = "NAME_FUZZY";
    public const string OrganizationExact = "ORGANIZATION_EXACT";
    public const string OrganizationFuzzy = "ORGANIZATION_FUZZY";
    public const string DobExact = "DOB_EXACT";
    public const string DobConflict = "DOB_CONFLICT";
    public const string IdentifierExact = "IDENTIFIER_EXACT";
    public const string IdentifierConflict = "IDENTIFIER_CONFLICT";
    public const string PhoneExact = "PHONE_EXACT";
    public const string EmailExact = "EMAIL_EXACT";
    public const string AddressExact = "ADDRESS_EXACT";
    public const string AddressFuzzy = "ADDRESS_PARTIAL";
    public const string SourceValueInvalid = "SOURCE_VALUE_INVALID";
    public const string SourceValueAmbiguous = "SOURCE_VALUE_AMBIGUOUS";
    public const string CandidateValueMissing = "CANDIDATE_VALUE_MISSING";
    public const string SourceValueMissing = "SOURCE_VALUE_MISSING";
    public const string CandidateProviderFailure = "CANDIDATE_PROVIDER_FAILURE";
    public const string ExactArtifactHash = "ARTIFACT_SHA256_EXACT";
    public const string BusinessKeyExact = "BUSINESS_KEY_EXACT";
}

public static class MatchComparisonMethods
{
    public const string Exact = "EXACT";
    public const string ComparisonKey = "COMPARISON_KEY";
    public const string NameSimilarity = "NAME_SIMILARITY";
    public const string OrganizationSimilarity = "ORGANIZATION_SIMILARITY";
    public const string AddressSimilarity = "ADDRESS_SIMILARITY";
}